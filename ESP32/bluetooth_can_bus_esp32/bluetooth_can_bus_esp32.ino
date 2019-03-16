#include "BluetoothSerial.h"
#include <esp32_can.h>


#if !defined(CONFIG_BT_ENABLED) || !defined(CONFIG_BLUEDROID_ENABLED)
#error Bluetooth is not enabled! Please run `make menuconfig` to and enable it
#endif

BluetoothSerial SerialBT;


void printFrame(CAN_FRAME *message)
{
      char buffer [50];
    int n, a=5, b=3;
    n=sprintf (buffer, "%03x", message->id);
    Serial.print("Sent: ");
  SerialBT.print(buffer);
  Serial.print(buffer);
  for (int i = 0; i < message->length; i++) {
    if(message->data.byte[i] < 16){
      SerialBT.print("0");
      Serial.print("0");
    }
    SerialBT.print(message->data.byte[i], HEX);
    Serial.print(message->data.byte[i], HEX);
  }
  SerialBT.println();
  Serial.println();
}

void gotHundred(CAN_FRAME *frame)
{
  Serial.print("Got special frame!  ");
  printFrame(frame);
}


void setup() {
  Serial.begin(115200);
  SerialBT.begin("ESP32_Can"); //Bluetooth device name
  Serial.println("The device started, now you can pair it with bluetooth!");

  CAN0.begin(500000);

  Serial.println("Ready ...!");
  CAN_FRAME txFrame;

  CAN0.watchFor(0x100, 0xF00); //setup a special filter
  CAN0.watchFor(); //then let everything else through anyway
  CAN0.setCallback(0, gotHundred); //callback on that first special filter
}

String inData;
unsigned char temp = 0;

void loop() {

  CAN_FRAME message;
  if (CAN0.read(message)) {
    printFrame(&message);
  }
}
