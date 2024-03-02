using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeNode : MonoBehaviour
{
    public struct Point
    {
        public Vector3 position { get; set; }
        public uint color { get; set; }
    }

    public Point pointData { get; set; }
    public TreeNode Left { get; set; }
    public TreeNode Right { get; set; }
    public TreeNode Parent { get; set; }
    public int left { get; set; }
    public int right { get; set; }
    public int k { get; set; }

    public string nodeName { get; set; }
    public int depth { get; set; }

    public TreeNode(Point point,TreeNode parentNode,int left,int right,int k,int depth)
    {
        this.pointData = point;
        this.Left = null;
        this.Right = null;
        this.Parent = parentNode;
        this.left = left;
        this.right = right;
        this.k = k;
        this.nodeName = left.ToString() + "+" + k.ToString() + "+" + right.ToString();
        this.depth = depth;
    }

    // ノードに値を追加するメソッド
    public void LeftInsert(Point point, TreeNode parentNode, int left, int right, int k,int depth)
    {
         Left = new TreeNode(point,parentNode,left,right,k,depth);
    }

    public void RightInsert(Point point, TreeNode parentNode, int left, int right, int k,int depth)
    {
        Right = new TreeNode(point,parentNode,left,right,k,depth);
    }
}
