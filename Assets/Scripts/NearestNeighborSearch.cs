using Pcx;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static TreeNode;

public class NearestNeighborSearch : MonoBehaviour
{
    [SerializeField] private PointCloudRenderer pointCloudRenderer;
    [SerializeField] private Color flashColor;

    private Point[] _pointData;

    private TreeNode _treeNode;

    private FlashPoint flashPoint;

    public enum Dimension
    {
        X,
        Y,
        Z
    }

    // Start is called before the first frame update
    void Start()
    {
        var pointBuffer = pointCloudRenderer.sourceData.computeBuffer;
        _pointData = new Point[pointBuffer.count];
        pointBuffer.GetData(_pointData);

        flashPoint = new FlashPoint(pointBuffer,_pointData,flashColor);

        //一番親のノードを決める
        int k = (int)Mathf.Floor(pointBuffer.count / 2);
        //追加された要素かどうかを保持するリストを作る
        bool[] addList = new bool[pointBuffer.count];
        //pointDataから小さい方からcenter番目の値を取得
        var point = PointQuickSelect(_pointData, 0, pointBuffer.count - 1, k, Dimension.X);
        _treeNode = new TreeNode(point,null,0, pointBuffer.count - 1, k,0);
        StartCoroutine(KdTreeCreate(_pointData, _treeNode, 0, pointBuffer.count - 1, k, (int)Dimension.X,0,addList));
    }

    IEnumerator KdTreeCreate(Point[] _pointData, TreeNode node, int left, int right, int k, int d, int depth,bool[] addList)
    {
        while (true)
        {
            Debug.Log("depth:" + depth);
            //現在のノードに記憶されたleft,right,kを取り出す
            left = node.left;
            right = node.right;
            k = node.k;
            Debug.Log("current nodeName:" + node.nodeName);

            d = (d + 1) % 3;
            var k1 = (int)Mathf.Floor((k - left) / 2)+left;
            var k2 = (int)Mathf.Ceil((right - k) / 2)+k;
            //左が空いていたら左に進む
            if (node.Left is null && addList[k1] == false)
            {
                var point1 = PointQuickSelect(_pointData, left, k, k1, (Dimension)d);
                node.LeftInsert(point1, node, left, k, k1,depth);
                Debug.Log("左の子ノードに" + point1 + "を追加しました");
                node = node.Left;
                depth++;
                addList[k1] = true;
                flashPoint.Add(k1);
                yield return null;
            }
            //左が空いていなかったら右に進む
            else if (addList[k2] == false)
            {
                var point2 = PointQuickSelect(_pointData, k, right, k2, (Dimension)d);
                node.RightInsert(point2, node, k, right, k2,depth);
                Debug.Log("右の子ノードに" + point2 + "を追加しました");
                node = node.Right;
                depth++;
                addList[k2] = true;
                flashPoint.Add(k2);
                yield return null;
            }
            else
            {
                if (node.Parent is null)
                {
                    var allClear = true;
                    for (int i = 0; i<addList.Length;i++)
                    {
                        if (addList[i] == false)
                        {
                            allClear = false;
                            Debug.Log(i + "番目が追加されていません");
                        }
                    }
                    if (allClear == true)
                        Debug.Log("点群がすべて正常に追加されました。");
;                    Debug.Log("KdTreeCreateの操作を終了します");
                    yield break;
                }
                Debug.Log("現在のノードが追加できないので親のノードに移行します。");
                node = node.Parent;
                d = (d - 1) % 3;
                depth--;
                yield return null;
            }
        }
    }

    Point PointQuickSelect(Point[] arr, int left, int right, int k, Dimension d)
    {
        if (left == right) // If the list contains only one element,
        {
            return arr[left]; // return that element
        }

        // Partition the array and get the position of the pivot
        int pivotIndex = PointPartition(arr, left, right, d);

        if (k == pivotIndex)
        {
            return arr[k];
        }
        else if (k < pivotIndex)
        {
            return PointQuickSelect(arr, left, pivotIndex - 1, k, d);
        }
        else
        {
            return PointQuickSelect(arr, pivotIndex + 1, right, k, d);
        }
    }

    int PointPartition(Point[] arr, int left, int right, Dimension d)
    {
        float pivot = GetPositionFromPoint(arr[right],d);
        int i = (left - 1);

        for (int j = left; j < right; j++)
        {
            if (GetPositionFromPoint(arr[j],d) < pivot)
            {
                i++;
                PointSwap(arr, i, j);
            }
        }

        PointSwap(arr, i + 1, right);
        return i + 1;
    }

    void PointSwap(Point[] arr, int i, int j)
    {
        Point temp = arr[i];
        arr[i] = arr[j];
        arr[j] = temp;
    }

    private static float GetPositionFromPoint(Point point, Dimension d)
    {
        switch (d)
        {
            case Dimension.X: return point.position.x;
            case Dimension.Y: return point.position.y;
            case Dimension.Z: return point.position.z;
            default: throw new ArgumentOutOfRangeException(nameof(d), "Invalid dimension");
        }
    }
}
