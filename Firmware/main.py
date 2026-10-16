# Hellboard firmware entry point. Button assignment: config.json; stratagem data: stratagems.json.
# Ctrl-C over the serial port stops the program (used by the upload tool / updater, decision F8).

import app

app.App().run()
