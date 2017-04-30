#include <Servo.h>

Servo myservo;  // create servo object to control a servo
int ACCELERATOR_PIN = 12;

void setup() {


  Serial.begin(9600);
  //while (!Serial) {
  //  ; // wait for serial port to connect. Needed for native USB port only
  //}

  // initialize digital pin LED_BUILTIN as an output.
  pinMode(LED_BUILTIN, OUTPUT);

  // accelerator pin
  pinMode(ACCELERATOR_PIN, OUTPUT);      // sets the digital pin as output
}


void keepSpeed(int speed, int steps) {
  for (int s = 0; s < steps; s++) {
    myservo.write(speed);
    delay(10);
  }
}

void turnLED(int val, int delay_time = 100){
    if(val > 0){
      digitalWrite(LED_BUILTIN, HIGH);   // LED on
    }
    else{
      digitalWrite(LED_BUILTIN, LOW);   // LED off
    }
    delay(delay_time);
}

void blinkLED(){
  turnLED(1);
  turnLED(0);
}

// make sure accelerator is running before calling this method
void shootOnce() {
  // SERVO: RELOAD
  int stopPos = 94;
  int offset = 15;

  // forward
  myservo.attach(9);  // attaches the servo on pin 9 to the servo object
  keepSpeed(stopPos - offset, 50);
  myservo.detach();
  delay(300);

  // back
  myservo.attach(9);  // attaches the servo on pin 9 to the servo object
  keepSpeed(stopPos + offset, 50);
  myservo.detach();
  delay(300);
}

void loop() {

  // ACCELERATOR ON
  digitalWrite(ACCELERATOR_PIN, HIGH);   // on
  delay(1000); // some delay

  shootOnce();
  shootOnce();
  shootOnce();

  // ACCELERATOR OFF
  digitalWrite(ACCELERATOR_PIN, LOW);   // off
  delay(1000); // some delay

  // BLINK
  for (int i = 0; i < 30; i++) {
    blinkLED();
  }
}
