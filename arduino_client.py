import serial
import serial.tools.list_ports
import sys

arduino_ports = [p.device for p in serial.tools.list_ports.comports() if 'GENUINO' in p.description]

if not arduino_ports:
	raise IOError("No Arduino found")
if len(arduino_ports) > 1:
	warnings.warn("Multiple Arduino found - will use the first one")

ser = serial.Serial(arduino_ports[0])

# turn LED on arduino ON
if len(sys.argv):
	msg = sys.argv[1]
	if msg == '1' or msg == '0':	
		ser.write(msg)

