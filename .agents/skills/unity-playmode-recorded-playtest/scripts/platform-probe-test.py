import importlib.util
from pathlib import Path
import socket
import unittest

spec = importlib.util.spec_from_file_location("probe", Path(__file__).with_name("platform-probe.py"))
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)

class PlatformProbeTest(unittest.TestCase):
    def test_absolute_paths(self):
        for path in ["C:/world/run", r"C:\world\run", "/tmp/world", r"\\server\share\run"]:
            self.assertEqual(path.replace("\\", "/"), probe.absolute_path(path))
        for path in ["world/run", "C:relative", ""]:
            with self.assertRaises(ValueError):
                probe.absolute_path(path)

    def test_live_listener_is_not_free(self):
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            port = listener.getsockname()[1]
            listener.listen()
            with self.assertRaises(OSError):
                probe.check_port(port)
        self.assertIn("bindable", probe.check_port(port))

if __name__ == "__main__":
    unittest.main()
