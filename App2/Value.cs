using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Runtime.Serialization;

namespace TeslaSCAN {


  /* a Value is an item read from the car, or calculated.
   * Value contains a formula
   * Value is based on one or more CAN packets
   * Values are initated at the startup of the program, and doesn't care 
   * if they have been read or not, they exist anyways.
   * This means they will reserve packetId space for filters etc, even if
   * the car hasn't sent that packet yet.
   * That means these are placeholders for coming items in the tabs, even if 
   * the car hasn't sent it.
   * 
   * I am now facing the problem that if I want different sort orders, I can't 
   * store the index in these, because they are global/single instance across
   * tabs! How can I rearrange the orders, serialize and deserialize? Probably
   * will need some refactoring here.
   * 
   * */
  [System.Diagnostics.DebuggerDisplay("{ToString()}")]
  [DataContract]
  public class Value {
    [DataMember]
    public string name;
    public string unit;
    public string tag;
    public int index;
    static int count;
    public Func<byte[], double> formula;
    public List<int> packetId;

    public override string ToString() {
      return name;
    }

    public Value(string name, string unit, string tag, Func<byte[], double> formula, List<int> packetId, int index) {
      this.name = name;
      this.unit = unit;
      if (index == -1)
        this.index = count++;
      else this.index = index;
      this.formula = formula;
      this.tag = tag;
      this.packetId = packetId;
    }

    public Value() { } // for serializer
  }

}