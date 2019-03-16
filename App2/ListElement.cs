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

  /*
   * ListElement is a single item shown on the screen.
   * It is an descendant of a Value
   * A value can have many listitems, showing on different tabs.
   * All listitem instances of a single value will update when the Value changes,
   * but they can keep their individual settings for sort order and
   * visualization type.
   * Or at least, that's the goal.
   * 
   * I realize these two types should inherit eachother
   * and be more closely related.
   * 
   * */

  [System.Diagnostics.DebuggerDisplay("{ToString()}")]
  public class ListElement {
    CustomAdapter adapter;
    public int packetId;
    public string name;
    double value;
    public string unit;
    public int index;
    private double max, min;
    public bool changed;
    public bool selected;
    public string tag;
    public int viewType;
    public long timeStamp;

    public override string ToString() {
      return name + ":" + value;
    }

    public double Convert(double val, bool convertToImperial) {
      if (!convertToImperial)
        return val;
      if (unit.Trim().ToUpper() == "C" || unit == "zCC")
        return val * 1.8 + 32;
      if (unit == "Nm")
        return val * Parser.nm_to_ftlb;
      if (unit == "wh|km")
        return val * Parser.miles_to_km;
      if (unit.ToLower().Contains("km"))
        return val / Parser.miles_to_km;
      return val;
    }

    public string GetUnit(bool convertToImperial) {
      if (unit == "zVC")
        return "V";
      if (!convertToImperial) {
        if (unit == "zCC")
          return "C";
        return unit;
      }
      if (unit.Trim().ToUpper() == "C" || unit == "zCC")
        return "F";
      if (unit == "Nm")
        return "LbFt";
      if (unit == "wh|km")
        return "wh|mi";
      if (unit.ToLower().Contains("km"))
        return unit.ToLower().Replace("km", "mi");
      return unit;
    }

    public double GetValue(bool convertToImperial) {
      if (!convertToImperial)
        return value;
      else
        return Convert(value, convertToImperial);
    }

    public void SetValue(double val) {
      changed = value != val;
      value = val;
      if (value > max)
        max = value;
      if (value < min)
        min = value;
      UpdateLimits(value);
#if VERBOSE
            Console.WriteLine(this.name + " " + val);
#endif
    }

    public void UpdateLimits(double value) {
      if (double.IsInfinity(value) || double.IsNaN(value))
        return;
      if (unit == "wh|km" || unit == "wh|mi") {
        if (value > 1000)
          value = 1000;
        if (value < -1000)
          value = -1000;
      }
      if (!adapter.limits.ContainsKey(unit))
        adapter.limits[unit] = new ValueLimit(value);
      else {
        var l = adapter.limits[unit];
        if (value > l.max) {
          l.max = value;
          l.changed = true;
        }
        if (value < l.min) {
          l.min = value;
          l.changed = true;
        }
      }
    }

    public double GetMax(bool convert) {
      return Convert(max, convert);
    }
    public double GetMin(bool convert) {
      return Convert(min, convert);
    }

    public double GetGlobalMax(bool convert) {
      return Convert(adapter.limits[unit].max, convert);
    }
    public double GetGlobalMin(bool convert) {
      return Convert(adapter.limits[unit].min, convert);
    }

    public bool LimitsChanged() {
      return adapter.limits[unit].changed;
    }

    public ListElement(string name, string unit, string tag, int index, double value, CustomAdapter adapter, int packetId) {
      this.adapter = adapter;
      this.packetId = packetId;
      this.name = name;
      this.value = value;
      this.unit = unit;
      this.tag = tag;
      this.index = index;
      min = max = value;
      UpdateLimits(value);
      changed = true;
    }
  }
}

public class ValueLimit {
  public double min, max;
  public bool changed;
  public string ToString() {
    return min + "/" + max;
  }

  public ValueLimit(double value) {
    changed = true;
    min = max = value;
  }
}
