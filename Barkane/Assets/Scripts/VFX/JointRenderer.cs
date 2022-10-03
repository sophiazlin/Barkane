using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BarkaneEditor;

[ExecuteInEditMode]
public class JointRenderer : MonoBehaviour, IRefreshable
{
    /**
     * FOR THE CONTEXT OF JOINT VFX...
     * A and B always represent the identity of the square, A always across the joint from B, and vice versa
     * 1 and 2 always represent the side o fthe square, A1 and A2 are always on the same side of the joint (but different facing direction), same for B
     * Pair1 contains both A and B for side 1, Pair2 contains both A and B for side 2
     */

    [SerializeField] SquareRenderSettings squareRenderSettings; // for referencing the margin property

    /// <summary>
    /// paper square filters
    /// </summary>
    [SerializeField, HideInInspector] MeshFilter filterA1, filterA2, filterB1, filterB2;
    /// <summary>
    /// paper square renderers
    /// </summary>
    [SerializeField, HideInInspector] MeshRenderer rendererA1, rendererA2, rendererB1, rendererB2;
    [SerializeField, HideInInspector] (SquareSide, SquareSide) pair1, pair2;

    /// <summary>
    /// target filters
    /// </summary>
    [SerializeField] MeshFilter tFilterA1, tFilterA2, tFilterB1, tFilterB2;
    /// <summary>
    /// target renderers
    /// </summary>
    [SerializeField] MeshRenderer tRendererA1, tRendererA2, tRendererB1, tRendererB2;

    private enum FoldState
    {
        Overlap,
        Coplanar,
        Orthogonal,
        NonAdjacent
    }

    private FoldState foldState;

    /// <summary>
    /// Can be called manually in inspector or automatically by other scene editor utilities.
    /// </summary>
    /// <exception cref="UnityException"></exception>
    void IRefreshable.Refresh()
    {
        var parent = transform.parent.GetComponent<PaperJoint>();
        if (parent.PaperSqaures.Count < 2)
        {
            throw new UnityException($"Cannot refresh joints without enough adjacent squares: {parent.PaperSqaures.Count}");
        }

        SquareSide
            a1 = parent.PaperSqaures[0].TopHalf.GetComponent<SquareSide>(),
            a2 = parent.PaperSqaures[0].BottomHalf.GetComponent<SquareSide>(),
            b1 = parent.PaperSqaures[1].TopHalf.GetComponent<SquareSide>(),
            b2 = parent.PaperSqaures[1].BottomHalf.GetComponent<SquareSide>();

        filterA1 = parent.PaperSqaures[0].TopHalf.GetComponent<MeshFilter>();
        filterA2 = parent.PaperSqaures[0].BottomHalf.GetComponent<MeshFilter>();
        filterB1 = parent.PaperSqaures[1].TopHalf.GetComponent<MeshFilter>();
        filterB2 = parent.PaperSqaures[1].BottomHalf.GetComponent<MeshFilter>();

        rendererA1 = parent.PaperSqaures[0].TopHalf.GetComponent<MeshRenderer>();
        rendererA2 = parent.PaperSqaures[0].BottomHalf.GetComponent<MeshRenderer>();
        rendererB1 = parent.PaperSqaures[1].TopHalf.GetComponent<MeshRenderer>();
        rendererB2 = parent.PaperSqaures[1].BottomHalf.GetComponent<MeshRenderer>();

        filterA1 = parent.PaperSqaures[0].TopHalf.GetComponent<MeshFilter>();
        filterA2 = parent.PaperSqaures[0].BottomHalf.GetComponent<MeshFilter>();
        filterB1 = parent.PaperSqaures[1].TopHalf.GetComponent<MeshFilter>();
        filterB2 = parent.PaperSqaures[1].BottomHalf.GetComponent<MeshFilter>();

        rendererA1 = parent.PaperSqaures[0].TopHalf.GetComponent<MeshRenderer>();
        rendererA2 = parent.PaperSqaures[0].BottomHalf.GetComponent<MeshRenderer>();
        rendererB1 = parent.PaperSqaures[1].TopHalf.GetComponent<MeshRenderer>();
        rendererB2 = parent.PaperSqaures[1].BottomHalf.GetComponent<MeshRenderer>();

        (pair1, pair2, foldState) = FormPairs(a1, a2, b1, b2);

        UpdateReferences();
        UpdateGeometry();
    }

    private Mesh RectangleUnit()
        => new Mesh()
        {
            vertices = new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(1, 0, 1) },
            triangles = new int[] { 1, 2, 3, 2, 3, 4 },
            normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up }
        };

    private ((SquareSide, SquareSide), (SquareSide, SquareSide), FoldState) FormPairs(SquareSide a1, SquareSide a2, SquareSide b1, SquareSide b2)
    {
        switch(CoordUtils.DiffAxisCount(a1, b1))
        {
            // for overlapping case, just compare if the normals are opposite
            case 0:
                // pair a1 with b1, a2 with b2
                if (CoordUtils.RoundEquals(a1.transform.up, -b1.transform.up)) return ((a1, b1), (a2, b2), FoldState.Overlap);
                
                // the other pairing
                else if (CoordUtils.RoundEquals(a1.transform.up, -b2.transform.up)) return ((a1, b2), (a2, b1), FoldState.Overlap);
                
                // invalid
                else throw new UnityException("Cannot pair a1 with anything! (Overlapping Case)");
            
            // for coplanar case, just compare if the normals match up
            case 1:
                // pair a1 with b1, a2 with b2
                if (CoordUtils.RoundEquals(a1.transform.up, b1.transform.up)) return ((a1, b1), (a2, b2), FoldState.Coplanar);
                
                // the other pairing
                else if (CoordUtils.RoundEquals(a1.transform.up, b2.transform.up)) return ((a1, b2), (a2, b1), FoldState.Coplanar);
                
                // invalid
                else throw new UnityException("Can't pair a1 with anything! (Coplanar Case)");
            case 2:
                // for orthogonal case, compare if the "towards" direction is in the same way as the normal (for both sides)
                // this compares whether two sides are facing "inwards" or "outwards"
                var a2b = CoordUtils.AsV(CoordUtils.FromTo(a1, b1));
                var b2a = -a2b;
                
                // pair a1 with b1, a2 with b2
                if (Mathf.Sign(Vector3.Dot(a1.transform.up, a2b)) == Mathf.Sign(Vector3.Dot(b1.transform.up, b2a))) return ((a1, b1), (a2, b2), FoldState.Orthogonal);
                
                // the other pairing
                else if (Mathf.Sign(Vector3.Dot(a1.transform.up, a2b)) == Mathf.Sign(Vector3.Dot(b1.transform.up, a2b))) return ((a1, b2), (a2, b1), FoldState.Orthogonal);
                
                // invalid
                else throw new UnityException("Can't pair a1 with anything! (Orthogonal Case)");
            
            // somehow there are more than 3 axis different in a 3D space..?
            default:
                // this includes the possible 3 case when non of the coordinates are equal (the tiles aren't even adjacent!)
                throw new UnityException($"Joint { transform.parent } contains squares that aren't adjacent!");
        }
    }

    /// <summary>
    /// Update Joint appearance when the type/state of either side changes
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    public void UpdateReferences()
    {
        FillMaterial(tRendererA1, pair1.Item1.MaterialPrototype);
        FillMaterial(tRendererA2, pair2.Item1.MaterialPrototype);
        FillMaterial(tRendererB1, pair1.Item2.MaterialPrototype);
        FillMaterial(tRendererB2, pair2.Item2.MaterialPrototype);
    }

    private void FillMaterial(MeshRenderer renderer, Material materialPrototype)
    {
        var material = new Material(squareRenderSettings.creaseMaterial);
        material.SetColor("_Color", materialPrototype.GetColor("_Color"));
        renderer.sharedMaterials = new Material[] { material };
    }

    /// <summary>
    /// Update Joint appearance when the physical location/orientation of either side changes
    /// </summary>
    public void UpdateGeometry()
    {
        var creaseNorm = Vector3.zero;

        // initialize creaseNorm
        switch(foldState)
        {
            // norm goes inwards from the joint to the center of the overlapping tiles (i.e. the center of any one of them)
            case FoldState.Overlap:
                creaseNorm = (pair1.Item1.transform.position - transform.position).normalized;
                break;
            
            // norm follows the upwards direction of any tile side
            // since the crease deforms both +y and -y, there's no need to pick a particular side
            case FoldState.Coplanar:
                creaseNorm = pair1.Item1.transform.up;
                break;

            // norm is the average between the two upward directions of any pair
            // the first pair is picked just as convention, it doesn't guarantee the norm will face inward
            case FoldState.Orthogonal:
                creaseNorm = (pair1.Item1.transform.up + pair1.Item2.transform.up).normalized;
                break;

            // invalid
            case FoldState.NonAdjacent:
                throw new UnityException("Cannot create geometry for non-adjacent tiles across a joint");
        }

        if (creaseNorm.sqrMagnitude < .1f) throw new UnityException("Crease normal cannot be initialized due to non-adjacent tiles");
        
    }
}