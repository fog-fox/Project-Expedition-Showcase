using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class ActionStateVariant
{
    /*
    지정된 Action State에서 기본 Action의 대상 조건, 범위, 영역 형태 및 Effect를
    선택적으로 유지, 교체 또는 확장하기 위한 상태별 Variant 데이터를 관리하는 클래스
    */

    [Header("State")]
    [Tooltip("이 Variant가 적용되는 Action State ID.")]
    [SerializeField] private string m_StateId;

    [Header("Target Mode")]
    [Tooltip("활성화하면 기본 Action의 Target Mode 대신 아래 값을 사용.")]
    [SerializeField] private bool m_OverrideTargetMode;
    [SerializeField] private ActionTargetMode m_TargetMode = ActionTargetMode.Caster;

    [Header("Target Filter")]
    [Tooltip("활성화하면 기본 Action의 Target Filter 대신 아래 값을 사용.")]
    [SerializeField] private bool m_OverrideTargetFilter;
    [SerializeField] private ActionTargetFilter m_TargetFilter = ActionTargetFilter.Any;

    [Header("Target Layer")]
    [Tooltip("활성화하면 기본 Action의 Target Layer 대신 아래 값을 사용.")]
    [SerializeField] private bool m_OverrideTargetLayerMask;
    [SerializeField] private LayerMask m_TargetLayerMask;

    [Header("Radius")]
    [Tooltip("활성화하면 기본 Action의 범위 반경 대신 아래 값을 사용.")]
    [SerializeField] private bool m_OverrideRadius;
    [SerializeField, Min(0.1f)] private float m_Radius = 2.5f;

    [Header("Area Shape")]
    [Tooltip("활성화하면 기본 Action의 Area Shape 대신 아래 값을 사용.")]
    [SerializeField] private bool m_OverrideAreaShape;
    [SerializeField] private ActionAreaShape m_AreaShape = ActionAreaShape.Circle;

    [Tooltip("활성화하면 기본 Action의 Sector Angle 대신 아래 값을 사용.")]
    [SerializeField] private bool m_OverrideSectorAngle;
    [SerializeField, Range(1f, 360f)] private float m_SectorAngle = 90f;

    [Header("Effects")]
    [Tooltip("현재 상태에서 기본 Action Effect를 유지, 교체 또는 추가할지 결정.")]
    [FormerlySerializedAs("m_OverrideEffects")]
    [SerializeField] private ActionStateEffectMode m_EffectMode = ActionStateEffectMode.KeepBase;
    [SerializeField] private ActionEffectInfo[] m_Effects;

    public string StateId => m_StateId ?? string.Empty;

    public bool OverrideTargetMode => m_OverrideTargetMode;
    public ActionTargetMode TargetMode => m_TargetMode;

    public bool OverrideTargetFilter => m_OverrideTargetFilter;
    public ActionTargetFilter TargetFilter => m_TargetFilter;

    public bool OverrideTargetLayerMask => m_OverrideTargetLayerMask;
    public LayerMask TargetLayerMask => m_TargetLayerMask;

    public bool OverrideRadius => m_OverrideRadius;
    public float Radius => Mathf.Max(0.1f, m_Radius);

    public bool OverrideAreaShape => m_OverrideAreaShape;
    public ActionAreaShape AreaShape => m_AreaShape;

    public bool OverrideSectorAngle => m_OverrideSectorAngle;
    public float SectorAngle => Mathf.Clamp(m_SectorAngle, 1f, 360f);

    public ActionStateEffectMode EffectMode => m_EffectMode;
    public ActionEffectInfo[] Effects => m_Effects;

    //지정된 Action State ID와 현재 Variant가 일치하는지 확인
    public bool IsMatched(string a_StateId)
    {
        if (string.IsNullOrWhiteSpace(a_StateId)) return false;
        return StateId == a_StateId;
    } //public bool IsMatched()
} //public class ActionStateVariant