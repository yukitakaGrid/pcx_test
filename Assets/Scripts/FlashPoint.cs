using Pcx;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static TreeNode;

public class FlashPoint : MonoBehaviour
{
    private ComputeBuffer _pointDataBuffer;
    private Point[] _pointData;

    private List<ColorPoint> colorPointList;
    // Start is called before the first frame update

    private Color flashColor;

    struct ColorPoint
    {
        public float flashTime { get; set; }
        public int index { get; set; }
    }

    public FlashPoint(ComputeBuffer buffer,Point[] pointData,Color color)
    {
        _pointDataBuffer = buffer;
        _pointData = pointData;

        colorPointList = new List<ColorPoint>();
        flashColor = color;
    }

    public void Add(int index)
    {
        if(_pointDataBuffer.count <= index)
        {
            Debug.Log("FlashPoint.add:indexが点群の範囲外に指定されています");
            return;
        }

        var colorUint = EncodeColor(flashColor);
        _pointData[index].color = colorUint;
        _pointDataBuffer.SetData(_pointData);
        Debug.Log("change point color");

        //発行中のリストに追加
        ColorPoint colorPoint = new ColorPoint();
        colorPoint.index = index;
        colorPoint.flashTime = 5;
        colorPointList.Add(colorPoint);
    }

    /*public void run(Point[] pointData)
    {
        for(int i = 0; i < colorPointList.size; i++)
        {

        }
    }*/

    static uint EncodeColor(Color c)
    {
        const float kMaxBrightness = 16;

        var y = Mathf.Max(Mathf.Max(c.r, c.g), c.b);
        y = Mathf.Clamp(Mathf.Ceil(y * 255 / kMaxBrightness), 1, 255);

        var rgb = new Vector3(c.r, c.g, c.b);
        rgb *= 255 * 255 / (y * kMaxBrightness);

        return ((uint)rgb.x) |
               ((uint)rgb.y << 8) |
               ((uint)rgb.z << 16) |
               ((uint)y << 24);
    }
}
