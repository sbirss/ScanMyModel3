using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitShiftTest {
  class Program {

    static int[] GetFilter(List<string> list) {

      int[] intids = new int[list.Count];
      Int32 mask = 0, filter, resultMask = -1, resultFilter = -1;
      int i = 0;
      foreach (var id in list)
        intids[i++] = Int32.Parse(id, System.Globalization.NumberStyles.HexNumber);

      //mask = intids.Min();

      /*foreach (var j in intids)
        Console.WriteLine(Convert.ToString(j,2).PadLeft(11, '0') +"    "+ Convert.ToString(j, 16).ToUpper());*/

      bool testfailed;
      int maxPasses = 0x7FFFFFFF;
      long it = 0;
      long tests = 0;

      for (filter = intids.Max(); filter >= intids.Min(); filter--)
        for (mask = 0x7FF; mask >= 0x0; mask--) {
          testfailed = false;
          // now let's test it
          for (i = 0; i < intids.Count(); i++) {
            it++;
            if (!((intids[i] & mask) == (filter & mask))) {
              testfailed = true;
              break;
            }
          }
          if (testfailed) {
            continue;
          } else {
            int passes = 0;
            /*for (i = 0; i <= 0x7FF; i++) {
              tests++;
              if (((i & mask) == (filter & mask)))
                passes++;
            }*/
            int tempFilter = filter;
            for (i = 0; i < 11; i++) {
              tests++;
              if ((tempFilter & 1) == 0)
                passes++;
              tempFilter >>= 1;
            }
            if (passes < maxPasses) {
              resultFilter = filter;
              resultMask = mask;
              maxPasses = passes;
              /*Console.WriteLine(Convert.ToString(intids[0], 16));
              Console.WriteLine(Convert.ToString(intids[i] & mask, 2).PadLeft(11, '0'));*/
              /*Console.WriteLine(Convert.ToString(filter & mask, 2).PadLeft(11, '0'));
              Console.WriteLine(Convert.ToString(filter, 2).PadLeft(11, '0') + " filter " + Convert.ToString(filter, 16));
              Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0') + "  mask  " + Convert.ToString(mask, 16));*/
              break;
            }
          }
        }
      Console.WriteLine();
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}. {3,13:N0} iterations {4,13:N0} tests", maxPasses, resultFilter, resultMask, it, tests);
      //Console.WriteLine(Convert.ToString(resultFilter, 2).PadLeft(11, '0') );
      //Console.WriteLine(Convert.ToString(resultMask, 2).PadLeft(11, '0') );


      //Console.ReadLine();

      return new int[3] {
        resultMask,
        resultFilter,
        maxPasses};
    }

    static int[] GetFilterBrute(List<string> list) {

      int[] intids = new int[list.Count];
      Int32 mask = 0, filter, resultMask = -1, resultFilter = -1;
      int i = 0;
      foreach (var id in list)
        intids[i++] = Int32.Parse(id, System.Globalization.NumberStyles.HexNumber);

      bool testfailed;
      int maxPasses = 0x7FFFFFFF;
      long it=0;
      long tests = 0;

      //for (filter = intids.Max(); filter >= intids.Min(); filter--)
      //for (filter = 0x7FF; filter >= 0; filter--)
      //for (filter = 0x0; filter < 0xFFF; filter++)
      //for (mask = 0xFFF; mask >= 0x0; mask--) {
      //for (filter = 0x0; filter < 0xFFF; filter++)
      //for (mask = 0x0; mask < 0xFFF; mask++) {
      for (filter = 0x7FF; filter >= 0; filter--)
        for (mask = 0x7FF; mask >= 0x0; mask--) {
          testfailed = false;
          // now let's test it
          for (i = 0; i < intids.Count(); i++) {
            it++;
            if (!((intids[i] & mask) == (filter & mask))) {
              testfailed = true;
              break;
            }
          }
          if (testfailed) {
            continue;
          } else {
            int passes = 0;
            for (i = 0; i <= 0x7FF; i++) {
              tests++;
              if (((i & mask) == (filter & mask)))
                passes++;
            }
            if (passes < maxPasses) {
              resultFilter = filter;
              resultMask = mask;
              maxPasses = passes;
              /*Console.WriteLine(Convert.ToString(filter & mask, 2).PadLeft(11, '0'));
              Console.WriteLine(Convert.ToString(filter, 2).PadLeft(11, '0') + " filter " + Convert.ToString(filter, 16));
              Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0') + "  mask  " + Convert.ToString(mask, 16));*/
              //break;
            }
          }
        }
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}. {3,13:N0} iterations {4,13:N0} tests", maxPasses, resultFilter, resultMask, it, tests);
      return new int[3] {
        resultMask,
        resultFilter,
        maxPasses};
    }

    static public string GetCANFilter(List<string> items) {
      int[] intids = new int[items.Count];
      int i = 0;
      foreach (var id in items) {
        intids[i++] = Int32.Parse(id, System.Globalization.NumberStyles.HexNumber);
        //Console.WriteLine(Convert.ToString(intids[i-1], 2).PadLeft(11,'0'));
      } 

      int filter = intids[0];
      int mask = 0;

      foreach (var item in intids)
        for (int bit=0; bit<11; bit++) {
          i = item;
          if (((i >> bit) & 1) != ((filter >> bit) & 1)) {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
        }

      mask = ~mask & 0x7FF;
      Console.WriteLine("--------");
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask, 1, 1);
      return filter.ToString("X");
    }



    static void test(int input, int mask, int filter) {
      /*Console.WriteLine(Convert.ToString(input, 16) + ":\t");
      Console.WriteLine(Convert.ToString(input, 2).PadLeft(12, '0') + " input");
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(12, '0') + " mask");
      Console.WriteLine(Convert.ToString(filter, 2).PadLeft(12, '0') + " filter");
      Console.WriteLine(Convert.ToString((input & mask), 2).PadLeft(12, '0') + " input & mask");
      Console.WriteLine(Convert.ToString((filter & mask), 2).PadLeft(12, '0') + " filter & mask");
      Console.WriteLine(Convert.ToString((input & filter), 2).PadLeft(12, '0') + " input & filter");*/
      if ((input & mask) == (filter & mask))
        Console.WriteLine(Convert.ToString(input, 16) + " passed");
    }


    static void Main(string[] args) {

      List<string> ids = new List<string>();
      /*ids.Add("102");
      ids.Add("210");
      ids.Add("306");
      ids.Add("154");
      ids.Add("266");
      ids.Add("116");*/
      ids.Add("302");
      ids.Add("3D2");
      //ids.Add("382");
      ids.Add("562");
      //ids.Add("6F2");
      //ids.Add("232");

      //ids.Add("106");
      // front torque measured
      //ids.Add("1D4");
      //ids.Add("2E5");
      //ids.Add("145");
      //ids.Add("7F8");

      // steering angle
      //ids.Add("00E");
      // brake pedal
      //ids.Add("168");
      // range
      //ids.Add("338");

      Stopwatch brute = new Stopwatch();
      Stopwatch opti = new Stopwatch();

      Console.WriteLine("----------------------------------------------------------------");

      List<string> l = new List<string>();

      for (int i = 0; i < ids.Count; i++) {
        l.Add(ids[i]);

        opti.Start();
        GetFilter(l);
        opti.Stop();

        brute.Start();
        GetFilterBrute(l);
        brute.Stop();

        GetCANFilter(l);
      }

      /*while (ids.Any()) {
        opti.Start();
        var o=GetFilter(ids);
        opti.Stop();

        brute.Start();
        var b=GetFilterBrute(ids);
        brute.Stop();

        ids.RemoveAt(ids.Count - 1);
        //ids.RemoveAt(0);
      }*/


      Console.WriteLine("Opti  done in {0}. Press Enter ", opti.Elapsed.ToString());
      Console.WriteLine("Brute done in {0}. Press Enter ", brute.Elapsed.ToString());
      Console.ReadLine();

    }
  }
}
