"""Makes the add-on's pure modules importable as `segment` and `quiet`, and the package as `basiraBridge`."""

import os
import sys

TESTS_DIR = os.path.dirname(os.path.abspath(__file__))
GLOBAL_PLUGINS_DIR = os.path.join(os.path.dirname(TESTS_DIR), "basiraBridge", "globalPlugins")
PLUGIN_DIR = os.path.join(GLOBAL_PLUGINS_DIR, "basiraBridge")

for path in (PLUGIN_DIR, GLOBAL_PLUGINS_DIR):
	if path not in sys.path:
		sys.path.insert(0, path)
