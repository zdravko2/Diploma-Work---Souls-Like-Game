using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(Collider))]
public class HitBox : MonoBehaviour
{
    public string hitboxName = "Unnamed HitBox";
    public CharacterManager owner;
    private Collider hitCollider;
    public DamageData baseDamage;
    public DamageData damageMultiplier;
    public List<CharacterEffect> onHitEffects = new List<CharacterEffect>();
    private HashSet<CharacterManager> charactersHit = new HashSet<CharacterManager>();
    
    [Header("Debug")]
    public bool showHitBox = true;

    private void Awake()
    {
        Reset();
        
        Debug.Assert(hitboxName != "" && hitboxName != "Unnamed HitBox", $"HitBox on gameobject [{gameObject.name}] has no name set!", this);

        DisableHitBox();
    }

    void Reset()
    {
        hitCollider = GetComponent<Collider>();
        hitCollider.enabled = false;
        hitCollider.isTrigger = true;
        hitboxName = hitboxName != "" && hitboxName != "Unnamed HitBox" ? hitboxName : gameObject.name;
        gameObject.layer = LayerMask.NameToLayer("HitBox");
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyHit(other);
    }

    // private void OnTriggerStay(Collider other)
    // {
    //     TryApplyHit(other);
    // }
    
    Vector3 contactPoint = Vector3.zero;
    private void TryApplyHit(Collider other)
    {
        HurtBox hurtbox = other.GetComponent<HurtBox>();
        if (hurtbox == null) return;

        CharacterManager target = hurtbox.owner;
        if (target == null || charactersHit.Contains(target)) return;
        if (owner != null && (target == owner || target.characterGroup == owner.characterGroup)) return;
        if (target.combatManager.hasInvincibility) return;

        Vector3 point1 = hitCollider.ClosestPoint(other.transform.position);
        Vector3 point2 = other.ClosestPoint(transform.position);
        contactPoint = (point1 + point2) * 0.5f;
        
        baseDamage.attacker = owner;
        baseDamage.contactPoint = contactPoint;

        charactersHit.Add(target);
        hurtbox.ReceiveDamageAndEffects(baseDamage * damageMultiplier, onHitEffects);

        //string sourceName = owner != null ? owner.name : "environment";
        //Debug.Log($"{sourceName} hit {target.name}");
    }

    public void EnableHitBox()
    {
        hitCollider.enabled = true;
    }

    public void DisableHitBox()
    {
        hitCollider.enabled = false;
        charactersHit.Clear();
    }


    private void OnDrawGizmos()
    {
        DrawHitboxGizmos();
    }

    private void DrawHitboxGizmos()
    {
        Collider col = hitCollider != null ? hitCollider : GetComponent<Collider>();
        if (!showHitBox || col == null || !col.enabled) return;

        Gizmos.color = Color.red;

        switch (col)
        {
            case BoxCollider box:
                DrawBoxGizmo(box);
                break;

            case SphereCollider sphere:
                DrawSphereGizmo(sphere);
                break;

            case CapsuleCollider capsule:
                DrawCapsuleGizmo(capsule);
                break;

            case MeshCollider mesh:
                DrawMeshGizmo(mesh);
                break;

            default:
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                break;
        }
    }

    private void DrawBoxGizmo(BoxCollider box)
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = oldMatrix;
    }

    private void DrawSphereGizmo(SphereCollider sphere)
    {
        Vector3 center = sphere.transform.TransformPoint(sphere.center);
        float radius = sphere.radius * MaxAbsComponent(sphere.transform.lossyScale);
        Gizmos.DrawWireSphere(center, radius);
    }

    private void DrawCapsuleGizmo(CapsuleCollider capsule)
    {
        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 scale = AbsVector(t.lossyScale);

        Vector3 axis;
        Vector3 sideA;
        Vector3 sideB;
        float radiusScale;
        float heightScale;

        switch (capsule.direction)
        {
            case 0:
                axis = t.right;
                sideA = t.up;
                sideB = t.forward;
                radiusScale = Mathf.Max(scale.y, scale.z);
                heightScale = scale.x;
                break;

            case 1:
                axis = t.up;
                sideA = t.right;
                sideB = t.forward;
                radiusScale = Mathf.Max(scale.x, scale.z);
                heightScale = scale.y;
                break;

            default:
                axis = t.forward;
                sideA = t.right;
                sideB = t.up;
                radiusScale = Mathf.Max(scale.x, scale.y);
                heightScale = scale.z;
                break;
        }

        axis.Normalize();
        sideA.Normalize();
        sideB.Normalize();

        float radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * heightScale, radius * 2f);
        float halfStraight = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 top = center + axis * halfStraight;
        Vector3 bottom = center - axis * halfStraight;

        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);

        Gizmos.DrawLine(top + sideA * radius, bottom + sideA * radius);
        Gizmos.DrawLine(top - sideA * radius, bottom - sideA * radius);
        Gizmos.DrawLine(top + sideB * radius, bottom + sideB * radius);
        Gizmos.DrawLine(top - sideB * radius, bottom - sideB * radius);
    }

    private void DrawMeshGizmo(MeshCollider mesh)
    {
        if (mesh.sharedMesh == null)
        {
            Gizmos.DrawWireCube(mesh.bounds.center, mesh.bounds.size);
            return;
        }

        Gizmos.DrawWireMesh(
            mesh.sharedMesh,
            0,
            mesh.transform.position,
            mesh.transform.rotation,
            mesh.transform.lossyScale
        );
    }

    private float MaxAbsComponent(Vector3 v)
    {
        v = AbsVector(v);
        return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
    }

    private Vector3 AbsVector(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
