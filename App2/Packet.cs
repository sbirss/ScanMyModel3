//#define disablebluetooth
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

namespace TeslaSCAN {

  class Packet {
    public int id;
    Parser parser;
    public List<Value> values;
    public double currentMultiplexer;

    public Packet(int id, Parser parser) {
      this.id = id;
      this.parser = parser;
      values = new List<Value>();
    }
    public void AddValue(string name, string unit, string tag, Func<byte[], double> formula, int[] additionalPackets = null, int index = -1) {
      List<int> list = new List<int>();
      list.Add(id);
      if (additionalPackets!=null)
        foreach (int i in additionalPackets)
          list.Add(i);
      values.Add(new Value(name, unit, tag, formula, list, index));
    }
    public void Update(byte[] bytes) {
      foreach (var val in values)
        if (val.formula != null)
          try {
            parser.UpdateItem(val.name, val.unit, val.tag, val.index, val.formula(bytes), id); // This guy sorts by packet ID
          } catch (Exception e) { Console.WriteLine(e.ToString()); }
    }
  }
}