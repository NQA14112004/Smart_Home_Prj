"""Make `import bridge...` work when running pytest from the Pi5/ directory."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
