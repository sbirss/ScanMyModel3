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
using Android.Graphics;

namespace TeslaSCAN {
  [Register("com.emon.canbus.tesla.teslaScan.ArcView")]
  class ArcView : View {

    private Paint mPaints;
    private Paint mPaints2;
    private Paint mFramePaint;

    public float angle;
    public bool negative;
    public bool drawArc = true;


    public ArcView(Context context, Android.Util.IAttributeSet attrib) : base(context) {
      try {
        SetWillNotDraw(false);
        mPaints = new Paint();
        mPaints.AntiAlias = true;
        //mPaints.SetStyle(Paint.Style.Fill);
        mPaints.SetStyle(Paint.Style.Stroke);
        mPaints.StrokeWidth = (80);
        mPaints.Color = Color.Red;
        mPaints2 = new Paint();
        mPaints2.AntiAlias = true;
        //mPaints.SetStyle(Paint.Style.Fill);
        mPaints2.SetStyle(Paint.Style.Stroke);
        mPaints2.StrokeWidth = (4);
        mPaints2.Color = Color.DarkRed;
        mFramePaint = new Paint();
        mFramePaint.AntiAlias = true;
        mFramePaint.SetStyle(Paint.Style.Stroke);
        mFramePaint.StrokeWidth = (1);

        Invalidate();
      }
      catch (Exception e) { Console.WriteLine(e.ToString()); }
    }
  


  protected override void OnDraw(Canvas canvas) {
      try {
        //canvas.DrawColor(Color.White);
        //canvas.DrawCircle(Width / 2, Height, Width/2, mPaints);

        if (angle > 180)
          angle = 180;
        if (angle < 0)
          angle = 0;

        if (negative) {
          mPaints.Color = Color.Green;
          mPaints2.Color = Color.DarkGreen;
        }
        else {
          mPaints.Color = Color.Red;
          mPaints2.Color = Color.DarkRed;
        }
        RectF rect = new RectF(90, 90, Width - 90, Height * 2 - 90);
        canvas.DrawArc(rect, angle - 182, 4, false, mPaints);
        if (!negative) {
          rect = new RectF(30, 30, Width - 30, Height * 2 - 30);
          canvas.DrawArc(rect, 180, angle, false, mPaints2);
        }
        else {
          rect = new RectF(30, 30, Width - 30, Height * 2 - 30);
          canvas.DrawArc(rect, 0, angle - 180, false, mPaints2);
        }
      }
      catch (Exception e) { Console.WriteLine(e.ToString()); }
    }

  }
}