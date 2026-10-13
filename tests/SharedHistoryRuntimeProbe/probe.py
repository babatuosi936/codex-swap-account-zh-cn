"""Credential-free native Codex shared-history probe (never sends turn/start).

Fixtures, RPC transcript and evidence are kept under --output for inspection.
The probe intentionally uses no real profile, authentication, or conversation.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import queue
import sqlite3
import subprocess
import threading
import time


ALLOWED_METHODS = {"initialize", "initialized", "thread/start", "thread/read", "thread/resume", "thread/list", "thread/name/set"}


class AppServer:
    def __init__(self, executable: Path, home: Path, shared: Path, cwd: Path, label: str):
        self.label, self.events, self.stderr = label, [], []
        self.messages: queue.Queue = queue.Queue()
        self.next_id = 0
        self.process = subprocess.Popen(
            [str(executable), "app-server", "--stdio"],
            env=isolated_env(home, shared), cwd=cwd,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, encoding="utf-8", errors="replace", bufsize=1,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
        )
        def stdout_reader():
            for line in self.process.stdout:
                try:
                    message = json.loads(line)
                except json.JSONDecodeError:
                    message = {"non_json_stdout": line.rstrip()}
                self.events.append({"direction": "receive", "message": message})
                self.messages.put(message)
        def stderr_reader():
            for line in self.process.stderr:
                self.stderr.append(line.rstrip())
        threading.Thread(target=stdout_reader, daemon=True).start()
        threading.Thread(target=stderr_reader, daemon=True).start()

    def request(self, method: str, params: dict, timeout: float = 30) -> dict:
        if method not in ALLOWED_METHODS:
            raise ValueError("Probe forbids all inference/turn operations")
        self.next_id += 1
        message = {"id": self.next_id, "method": method, "params": params}
        self.events.append({"direction": "send", "message": message})
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                response = self.messages.get(timeout=deadline - time.monotonic())
            except queue.Empty:
                break
            if response.get("id") == self.next_id:
                return response
        raise TimeoutError(f"{self.label}: no response to {method}; exit={self.process.poll()}")

    def initialize(self):
        response = self.request("initialize", {"clientInfo": {
            "name": "shared-history-synthetic-probe", "version": "1"},
            "capabilities": {"experimentalApi": True}})
        self.events.append({"direction": "send", "message": {"method": "initialized"}})
        self.process.stdin.write('{"method":"initialized"}\n')
        self.process.stdin.flush()
        return response

    def close(self):
        if self.process.poll() is None:
            self.process.stdin.close()
            try:
                self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.process.terminate()
                self.process.wait(timeout=5)


def junction(link: Path, target: Path):
    if os.name != "nt":
        link.symlink_to(target, target_is_directory=True)
        return
    result = subprocess.run(["powershell", "-NoProfile", "-Command",
        "$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path $env:PROBE_LINK -Target $env:PROBE_TARGET | Out-Null"],
        env={**os.environ, "PROBE_LINK": str(link), "PROBE_TARGET": str(target)},
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode:
        raise RuntimeError("Fixture junction failed: " + result.stderr)


def inventory(root: Path):
    # Path.walk/recursive glob can revisit junctions; os.walk does not follow them.
    result = []
    for base, dirs, files in os.walk(root, followlinks=False):
        dirs[:] = [d for d in dirs if not Path(base, d).is_symlink() and not Path(base, d).is_junction()]
        for name in files:
            path = Path(base, name)
            result.append({"path": str(path.relative_to(root)), "bytes": path.stat().st_size})
    return result


def sqlite_summary(shared: Path):
    result = {}
    for db in shared.glob("*.sqlite"):
        conn = sqlite3.connect(db.as_uri() + "?mode=ro", uri=True)
        try:
            tables = [r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type='table'")]
            result[db.name] = {"tables": tables}
            if "threads" in tables:
                result[db.name]["threads"] = [dict(zip(["id", "rollout_path"], row)) for row in
                    conn.execute("SELECT id, rollout_path FROM threads")]
        finally:
            conn.close()
    return result


def isolated_env(home: Path, shared: Path) -> dict[str, str]:
    env = dict(os.environ)
    for key in list(env):
        upper = key.upper()
        if (upper.startswith(("CODEX_", "OPENAI_", "AZURE_OPENAI_", "ANTHROPIC_"))
                or any(s in upper for s in ("API_KEY", "TOKEN", "SECRET", "PASSWORD", "COOKIE"))):
            env.pop(key, None)
    env["CODEX_HOME"] = str(home)
    env["CODEX_SQLITE_HOME"] = str(shared)
    # A local loopback dead-end keeps incidental HTTP discovery offline.
    env["HTTP_PROXY"] = env["HTTPS_PROXY"] = env["ALL_PROXY"] = "http://127.0.0.1:9"
    env["NO_PROXY"] = ""
    return env


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--executable", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--schema-only", action="store_true")
    parser.add_argument("--share-writer-locks", action="store_true")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    home = output / "schema-home"
    home.mkdir()
    shared = output / "shared-sqlite"
    shared.mkdir()
    schema = output / "schema"
    schema.mkdir()
    capture = subprocess.run(
        [str(args.executable), "app-server", "generate-json-schema", "--out", str(schema)],
        env=isolated_env(home, shared), cwd=output, capture_output=True, text=True,
        encoding="utf-8", errors="replace", timeout=30,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
    )
    (output / "schema-generation.json").write_text(json.dumps({
        "returncode": capture.returncode, "stdout": capture.stdout, "stderr": capture.stderr,
    }, indent=2), encoding="utf-8")
    print(json.dumps({"schema_directory": str(schema), "returncode": capture.returncode}))
    if capture.returncode:
        raise RuntimeError("Native schema generation failed; see isolated capture")
    if args.schema_only:
        return
    fixture_cwd = output / "synthetic-workspace"
    fixture_cwd.mkdir()
    raw = output / "shared-raw"
    raw.mkdir()
    for name in ("sessions", "archived_sessions"):
        (raw / name).mkdir()
    homes = [output / "home-a", output / "home-b"]
    lock_root = output / "shared-writer-locks"
    if args.share_writer_locks:
        lock_root.mkdir()
    config = '''model = "synthetic-local-probe"
model_provider = "probe"
check_for_update_on_startup = false
[analytics]
enabled = false
[model_providers.probe]
name = "Synthetic local probe"
base_url = "http://127.0.0.1:9"
wire_api = "responses"
requires_openai_auth = false
'''
    for item in homes:
        item.mkdir()
        (item / "config.toml").write_text(config, encoding="utf-8")
        for name in ("sessions", "archived_sessions"):
            junction(item / name, raw / name)
        if args.share_writer_locks:
            junction(item / "thread-writer-locks", lock_root)
    result = {"fixture_root": str(output), "native_executable": str(args.executable),
        "inference_requests": 0, "credentials_provided": False,
        "model_provider_endpoint": "http://127.0.0.1:9", "share_writer_locks": args.share_writer_locks,
        "responses": {},
        "limitations": ["No turn/start or model inference is permitted; active-turn coordination is not exercised."]}
    servers = []
    try:
        a = AppServer(args.executable, homes[0], shared, fixture_cwd, "A")
        servers.append(a)
        result["responses"]["initialize_a"] = a.initialize()
        b = AppServer(args.executable, homes[1], shared, fixture_cwd, "B")
        servers.append(b)
        result["responses"]["initialize_b"] = b.initialize()
        options = {"cwd": str(fixture_cwd), "model": "synthetic-local-probe",
            "modelProvider": "probe", "approvalPolicy": "never", "sandbox": "read-only",
            "baseInstructions": "Synthetic local storage probe. Do not start a model turn."}
        start = a.request("thread/start", {**options, "ephemeral": False})
        result["responses"]["start_a"] = start
        if "error" in start:
            raise RuntimeError("thread/start rejected: " + start["error"]["message"])
        tid = start["result"]["thread"]["id"]
        result["synthetic_thread_id"] = tid
        result["responses"]["set_synthetic_name"] = a.request("thread/name/set", {
            "threadId": tid, "name": "Synthetic shared-history fixture"})
        time.sleep(1)
        result["inventory_after_start"] = inventory(output)
        result["sqlite_after_start"] = sqlite_summary(shared)
        result["responses"]["read_b"] = b.request("thread/read", {"threadId": tid, "includeTurns": False})
        result["responses"]["resume_b_while_a_alive"] = b.request("thread/resume", {"threadId": tid})
        time.sleep(1)
        result["inventory_after_concurrent_resume"] = inventory(output)
        result["responses"]["read_a_after_b_resume"] = a.request("thread/read", {"threadId": tid, "includeTurns": False})
        a.close()
        result["inventory_after_seed_flush"] = inventory(output)
        result["responses"]["resume_b_to_materialize_seed"] = b.request("thread/resume", {"threadId": tid})
        b.close()
        a = AppServer(args.executable, homes[0], shared, fixture_cwd, "A-restarted")
        servers.append(a)
        result["responses"]["initialize_a_restarted"] = a.initialize()
        result["responses"]["resume_a_from_flushed_seed"] = a.request("thread/resume", {"threadId": tid})
        result["responses"]["update_name_a_after_seed"] = a.request("thread/name/set", {
            "threadId": tid, "name": "Synthetic owner metadata updated"})
        b = AppServer(args.executable, homes[1], shared, fixture_cwd, "B-restarted")
        servers.append(b)
        result["responses"]["initialize_b_restarted"] = b.initialize()
        result["responses"]["read_b_after_seed_flush"] = b.request("thread/read", {"threadId": tid, "includeTurns": False})
        result["responses"]["resume_b_while_a_owns_flushed_thread"] = b.request("thread/resume", {"threadId": tid})
        result["inventory_while_both_resumed"] = inventory(output)
        lock_contents = {}
        for root in (lock_root,) if args.share_writer_locks else (homes[0] / "thread-writer-locks", homes[1] / "thread-writer-locks"):
            for path in root.glob("*"):
                if path.is_file() and path.stat().st_size < 16384:
                    try:
                        lock_contents[str(path.relative_to(output))] = path.read_text(encoding="utf-8", errors="replace")
                    except PermissionError:
                        lock_contents[str(path.relative_to(output))] = {"exclusive_file_lock_prevented_read": True}
        result["writer_lock_contents_while_both_resumed"] = lock_contents
        a.close()
        result["responses"]["resume_b_after_a_exit"] = b.request("thread/resume", {"threadId": tid})
        raw_files = list((raw / "sessions").rglob("*.jsonl"))
        result["checks"] = {
            "one_shared_raw_file": len(raw_files) == 1,
            "shared_sqlite_thread_index": bool(result["sqlite_after_start"].get("state_5.sqlite")),
            "cross_home_read_from_flushed_seed": "result" in result["responses"]["read_b_after_seed_flush"],
            "cross_home_new_thread_read_while_creator_alive": "result" in result["responses"]["read_b"],
            "cross_home_metadata_update_visible": result["responses"]["read_b_after_seed_flush"].get("result", {}).get("thread", {}).get("name") == "Synthetic owner metadata updated",
            "a_accepts_direct_input": result["responses"]["resume_a_from_flushed_seed"].get("result", {}).get("thread", {}).get("canAcceptDirectInput"),
            "b_accepts_direct_input_while_a_alive": result["responses"]["resume_b_while_a_owns_flushed_thread"].get("result", {}).get("thread", {}).get("canAcceptDirectInput"),
            "b_resume_rejected_for_active_writer": "already has an active writer" in result["responses"]["resume_b_while_a_owns_flushed_thread"].get("error", {}).get("message", ""),
            "b_can_resume_after_a_exit": "result" in result["responses"]["resume_b_after_a_exit"],
            "state_database_only_in_shared_root": not any((home / "state_5.sqlite").exists() for home in homes),
        }
        expected = (result["checks"]["b_resume_rejected_for_active_writer"] if args.share_writer_locks
                    else result["checks"]["b_accepts_direct_input_while_a_alive"] is True)
        required = ("one_shared_raw_file", "cross_home_read_from_flushed_seed", "cross_home_new_thread_read_while_creator_alive", "cross_home_metadata_update_visible", "b_can_resume_after_a_exit", "state_database_only_in_shared_root")
        result["outcome"] = "passed" if expected and all(result["checks"][key] for key in required) else "unexpected_native_behavior"
    except Exception as exc:
        result["outcome"] = "probe_failed"
        result["failure"] = str(exc)
    finally:
        for server in servers:
            server.close()
            (output / (server.label + "-rpc.json")).write_text(json.dumps(server.events, indent=2), encoding="utf-8")
            (output / (server.label + "-stderr.txt")).write_text("\n".join(server.stderr), encoding="utf-8")
        result["final_inventory"] = inventory(output)
        result["final_sqlite"] = sqlite_summary(shared)
        (output / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        print(json.dumps({"outcome": result.get("outcome"), "result": str(output / "result.json")}))
    if result.get("outcome") != "passed":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
