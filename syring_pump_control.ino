#include <TMC2209.h>
#include <AccelStepper.h>

// Define TMC2209 settings
#define EN_PIN 23           // Enable
#define DIR_PIN 21          // Direction
#define STEP_PIN 22         // Step
#define HOME_SWITCH_PIN 19  // Endstop switch
#define R_SENSE 0.11f       // Sense resistor value

#define NORMAL_CURRENT 1500
#define JOGGING_CURRENT 1500

#define MAX_MICROSTEPS 256
#define FULL_STEPS_PER_REV 200  // Full steps per revolution
#define SCREW_PITCH 8.0         // Lead screw pitch (mm per revolution)
#define FULL_STEPS_PER_MM (FULL_STEPS_PER_REV * MAX_MICROSTEPS / SCREW_PITCH)


HardwareSerial SerialPort2(2);

TMC2209 stepper_driver;

// Define AccelStepper instance
AccelStepper stepper(AccelStepper::DRIVER, STEP_PIN, DIR_PIN);

bool estopActivated = false;

// Number of steps per revolution
const long stepsPerRevolution = FULL_STEPS_PER_REV * MAX_MICROSTEPS / 60;
float targetSpeed = 0;
unsigned long lastUpdate = 0;
const unsigned long updateInterval = 1000; // Update every second
float jogSpeed = 8;

void setup() {
  // Setup serial communication
  Serial.begin(115200);
  SerialPort2.begin(115200, SERIAL_8N1, 16, 17);  // TMC2209 serial communication

  // Setup TMC2209
  pinMode(EN_PIN, OUTPUT);
  pinMode(HOME_SWITCH_PIN, INPUT_PULLUP);  // Configure endstop switch pin
  digitalWrite(EN_PIN, HIGH);              // Disable the driver initially

  stepper_driver.setup(SerialPort2);
  stepper_driver.setRunCurrent(NORMAL_CURRENT);
  stepper_driver.setMicrostepsPerStep(MAX_MICROSTEPS);
  stepper_driver.enableAutomaticCurrentScaling();
  stepper_driver.enableAutomaticGradientAdaptation();
  stepper_driver.enableStealthChop();
  stepper_driver.setHardwareEnablePin(EN_PIN);
  stepper_driver.enable();
  stepper_driver.disableCoolStep();

  // Setup AccelStepper
  stepper.setMaxSpeed(50 * stepsPerRevolution);     // Set the maximum speed in steps per second
  stepper.setAcceleration(1 * stepsPerRevolution);  // Set the acceleration in steps per second^2

  // Print TMC2209 settings to verify
  //printTMC2209Settings();

  Serial.println("Setup complete.");
}

void loop() {
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n');
    Serial.print("Received command: ");
    Serial.println(command);
    handleCommand(command);
  }

  stepper.runSpeed();  // Ensure the stepper motor runs according to the set speed and acceleration

  unsigned long currentMillis = millis();
  if (currentMillis - lastUpdate >= updateInterval) {
    lastUpdate = currentMillis;
    updateStatus();
  }
}

void handleCommand(String command) {
    if (command.startsWith("SET_SPEED")) {
        Serial.println("Setting speed.");
        sscanf(command.c_str() + 10, "%f", &targetSpeed);

        stepper.setMaxSpeed(targetSpeed);
        stepper.setSpeed(targetSpeed);

        Serial.print("Set target speed in steps per second: ");
        Serial.println(targetSpeed);
    } else if (command == "START") {
        Serial.println("Starting stepper.");
        stepper_driver.enable();
        stepper.setSpeed(targetSpeed);
    } else if (command == "STOP") {
        Serial.println("Stopping stepper.");
        stepper.setSpeed(0);
        stepper.stop();  // Stop the motor
        stepper_driver.disable();
    } else if (command == "JOG_FORWARD") {
        Serial.println("Jogging forward.");
        stepper_driver.enable();
        stepper.setMaxSpeed(jogSpeed * stepsPerRevolution);  // Explicitly set max speed for jogging
        stepper.setAcceleration(.5 * stepsPerRevolution);    // Explicitly set acceleration for jogging
        runMotorWithSCurve(jogSpeed * stepsPerRevolution, .5 * stepsPerRevolution, 500); // Adjust timeInterval as needed
    } else if (command == "JOG_BACKWARD") {
        Serial.println("Jogging backward.");
        stepper_driver.enable();
        stepper.setMaxSpeed(jogSpeed * stepsPerRevolution);  // Explicitly set max speed for jogging
        stepper.setAcceleration(.5 * stepsPerRevolution);    // Explicitly set acceleration for jogging
        runMotorWithSCurve(-jogSpeed * stepsPerRevolution, .5 * stepsPerRevolution, 500); // Adjust timeInterval as needed
    } else if (command == "STOP_JOG") {
        Serial.println("Stopping jog.");
        runMotorWithSCurve(0, .5 * stepsPerRevolution, 500);
        stepper.setSpeed(0);
        stepper.stop();
        setNormalCurrent();
        stepper_driver.disable();
    } else if (command == "HOME") {
        Serial.println("Homing motor.");
        homeMotor();
    } else if (command == "ESTOP") {
        Serial.println("Emergency stop.");
        stepper.setSpeed(0);
        stepper.stop();
        stepper_driver.disable();
        estopActivated = true;
    } else if (command == "ENABLE") {
        stepper_driver.enable();
    } else if (command == "DISABLE") {
        stepper_driver.disable();
    } else if (command == "SETPOS_0") {
        stepper.setCurrentPosition(0);
    } else if (command.startsWith("JOGSPEED")) {
        Serial.println("Setting Jog Speed");
        sscanf(command.c_str() + 9, "%f", &jogSpeed);
        Serial.print("Set Jog Speed: ");
        Serial.println(jogSpeed);
    }
}



void homeMotor() {
    // Step 1: Move backwards until the switch is pressed
    Serial.println("Homing: Moving backwards until switch is pressed...");
    stepper_driver.enable();

    // Explicitly set speed and acceleration for homing
    stepper.setMaxSpeed(5 * stepsPerRevolution);
    stepper.setAcceleration(1 * stepsPerRevolution);

    // Use S-curve acceleration to move backwards
    runMotorWithSCurve(-5 * stepsPerRevolution, 1 * stepsPerRevolution, 2000); // Accelerate over 2 seconds

    while (!isHomeSwitchPressed()) {
        stepper.runSpeed();
        if (estopActivated) {
            return;
        }
    }

    stepper.setSpeed(0);
    stepper.stop();  // Stop the motor
    delay(500);      // Short delay after stopping

    // Step 2: Move forward 2 revolutions
    Serial.println("Homing: Moving forward 2 revolutions...");
    stepper.setAcceleration(1 * stepsPerRevolution);
    stepper.move(2 * stepsPerRevolution);

    while (stepper.distanceToGo() != 0) {
        stepper.run();
    }

    stepper.setSpeed(0);
    stepper.stop();  // Stop the motor
    delay(500);      // Short delay after stopping

    // Step 3: Slowly move backwards until the switch is pressed again
    Serial.println("Homing: Slowly moving backwards until switch is pressed again...");

    runMotorWithSCurve(-1 * stepsPerRevolution, 0.5 * stepsPerRevolution, 1000); // Accelerate over 1 second

    while (!isHomeSwitchPressed()) {
        stepper.runSpeed();
        if (estopActivated) {
            return;
        }
    }

    stepper.setSpeed(0);
    stepper.stop();  // Stop the motor
    delay(500);      // Short delay after stopping

    stepper.setCurrentPosition(0);  // Set current position as zero
    stepper_driver.disable();
    stepper.setSpeed(0);
    Serial.println("Homing complete.");
}



void runMotorWithSCurve(float targetSpeed, float acceleration, float timeInterval) {
    float currentSpeed = stepper.speed();  // Start with the current speed
    unsigned long startTime = millis();
    unsigned long elapsedTime = 0;

    while (elapsedTime < timeInterval) {
        elapsedTime = millis() - startTime;

        // Apply S-curve logic: Adjust the acceleration curve for a smoother transition
        float progress = (float)elapsedTime / timeInterval;
        float curveFactor = 3 * pow(progress, 2) - 2 * pow(progress, 3); // S-curve formula (cubic)

        float speed = currentSpeed + (targetSpeed - currentSpeed) * curveFactor;
        stepper.setSpeed(speed);
        stepper.runSpeed();  // Run the motor at the calculated speed

        // Exit if e-stop is activated
        if (estopActivated) {
            stepper.setSpeed(0);
            stepper_driver.disable();
            return;
        }
    }

    // Set the final speed to target speed
    stepper.setSpeed(targetSpeed);
}


bool isHomeSwitchPressed() {
  return digitalRead(HOME_SWITCH_PIN) == HIGH;  // Assuming LOW means pressed
}

void setNormalCurrent() {
  stepper_driver.setRunCurrent(NORMAL_CURRENT);  // Set motor current to normal value
  Serial.println("Current set to normal");
}

void setJoggingCurrent() {
  stepper_driver.setRunCurrent(JOGGING_CURRENT);  // Set motor current to jogging value
  Serial.println("Current set to jogging");
}

void updateStatus() {
  long currentPosition = stepper.currentPosition();
  float currentRPM = (stepper.speed() / stepsPerRevolution); // Convert speed to RPM

  Serial.print("Current position in steps: ");
  Serial.println(currentPosition);
  Serial.print("Current RPM: ");
  Serial.println(currentRPM);
}

/*
void printTMC2209Settings() {
  TMC2209::Settings settings = stepper_driver.getSettings();

  Serial.println("*************************");
  Serial.println("TMC2209 Settings:");
  Serial.print("settings.irun_percent = ");
  Serial.println(settings.irun_percent);
  Serial.print("settings.irun_register_value = ");
  Serial.println(settings.irun_register_value);
  Serial.print("settings.ihold_percent = ");
  Serial.println(settings.ihold_percent);
  Serial.print("settings.ihold_register_value = ");
  Serial.println(settings.ihold_register_value);
  Serial.print("settings.iholddelay_percent = ");
  Serial.println(settings.iholddelay_percent);
  Serial.print("settings.iholddelay_register_value = ");
  Serial.println(settings.iholddelay_register_value);
  Serial.print("settings.automatic_current_scaling_enabled = ");
  Serial.println(settings.automatic_current_scaling_enabled);
  Serial.print("settings.automatic_gradient_adaptation_enabled = ");
  Serial.println(settings.automatic_gradient_adaptation_enabled);
  Serial.print("settings.pwm_offset = ");
  Serial.println(settings.pwm_offset);
  Serial.print("settings.pwm_gradient = ");
  Serial.println(settings.pwm_gradient);
  Serial.print("settings.cool_step_enabled = ");
  Serial.println(settings.cool_step_enabled);
  Serial.print("settings.analog_current_scaling_enabled = ");
  Serial.println(settings.analog_current_scaling_enabled);
  Serial.print("settings.internal_sense_resistors_enabled = ");
  Serial.println(settings.internal_sense_resistors_enabled);
  Serial.println("*************************");
  Serial.println();

  // Retrieve and print hardware status
  Serial.println("*************************");
  Serial.println("hardwareDisabled()");
  bool hardware_disabled = stepper_driver.hardwareDisabled();
  Serial.print("hardware_disabled = ");
  Serial.println(hardware_disabled);
  Serial.println("*************************");
  Serial.println();

  
  // Retrieve and print status
  Serial.println("*************************");
  Serial.println("getStatus()");
  TMC2209::Status status = stepper_driver.getStatus();
  Serial.print("status.over_temperature_warning = ");
  Serial.println(status.over_temperature_warning);
  Serial.print("status.over_temperature_shutdown = ");
  Serial.println(status.over_temperature_shutdown);
  Serial.print("status.short_to_ground_a = ");
  Serial.println(status.short_to_ground_a);
  Serial.print("status.short_to_ground_b = ");
  Serial.println(status.short_to_ground_b);
  Serial.print("status.low_side_short_a = ");
  Serial.println(status.low_side_short_a);
  Serial.print("status.low_side_short_b = ");
  Serial.println(status.low_side_short_b);
  Serial.print("status.open_load_a = ");
  Serial.println(status.open_load_a);
  Serial.print("status.open_load_b = ");
  Serial.println(status.open_load_b);
  Serial.print("status.over_temperature_120c = ");
  Serial.println(status.over_temperature_120c);
  Serial.print("status.over_temperature_143c = ");
  Serial.println(status.over_temperature_143c);
  Serial.print("status.over_temperature_150c = ");
  Serial.println(status.over_temperature_150c);
  Serial.print("status.over_temperature_157c = ");
  Serial.println(status.over_temperature_157c);
  Serial.print("status.current_scaling = ");
  Serial.println(status.current_scaling);
  Serial.print("status.stealth_chop_mode = ");
  Serial.println(status.stealth_chop_mode);
  Serial.print("status.standstill = ");
  Serial.println(status.standstill);
  Serial.println("*************************");
  Serial.println();
}
*/
