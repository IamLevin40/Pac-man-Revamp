using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "NewAttackInfo", menuName = "BrawlMode/AttackInfo")]
public class AttackInfo : ScriptableObject {
    public int damage;
    public float stunDuration;
    public float chargeDuration;
    public float hitDuration;
    public float missDuration;
    public float attackCooldown;
    public Vector2 rootVelocity;
    public Vector2 otherVelocity;
    public Vector2 attackBoxOffset;
    public Vector2 attackBoxSize;
    public AttackInfo nextAttack;
    public AttackCategory attackCategory;
    public bool shakeCam;
    public float camShakeStrength;
    public float camShakeDuration;
}