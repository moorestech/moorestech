"""Native filesystem and TCP probes used by the Bash playtest runner."""
import ntpath
import posixpath
import socket
import sys


def absolute_path(value):
    # Unity returns native drive-rooted paths on Windows.
    if posixpath.isabs(value) or (ntpath.isabs(value) and ntpath.splitdrive(value)[0]):
        return value.replace("\\", "/")
    raise ValueError("Expected an absolute Unity result path: " + value)


def check_port(port):
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
        if sys.platform == "win32":
            probe.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        probe.bind(("0.0.0.0", port))
    return "port " + str(port) + " verified bindable"


if __name__ == "__main__":
    # OS resources are the boundary: probe failures are explicit, never treated as a free port.
    try:
        if sys.argv[1] == "path":
            print(absolute_path(sys.argv[2]))
        elif sys.argv[1] == "port":
            print(check_port(int(sys.argv[2])))
        else:
            raise ValueError("Unknown probe command")
    except (OSError, ValueError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
