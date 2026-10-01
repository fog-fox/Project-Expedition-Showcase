using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ActionTriggerController : MonoBehaviour
{
    /*
    대상에게 등록된 Action Trigger의 지속시간과 발동 조건을 관리하고
    Damage Event에 반응하여 Reaction Action과 Resource 생명주기를 처리하는 컨트롤러
    */

    private const string DefaultReactionSourceKey = "TriggeredAction";

    private sealed class ActiveTrigger
    {
        public ActionTriggerDefinition m_Definition;

        public GameObject m_SourceObject;
        public string m_SourceName;

        public CharicRuntimeAssetProvider m_AssetProvider;
        public StackResourceReservationHandle m_ResourceHandle;

        public float m_RemainingDuration;
        public int m_RemainingTriggerCount;

        public bool m_IsExecuting;
        public bool m_IsPendingRemoval;
    }

    private readonly List<ActiveTrigger> m_ActiveTriggers =
        new List<ActiveTrigger>();

    private readonly List<ActiveTrigger> m_RemoveBuffer =
        new List<ActiveTrigger>();

    private ActionTriggerEventHub m_EventHub;
    private ActionStatusController m_StatusController;
    private CombatTarget m_CombatTarget;

    private int m_EventProcessingDepth;

    public int ActiveTriggerCount => m_ActiveTriggers.Count;

    //Trigger 실행에 필요한 대상 컴포넌트 탐색 및 Event 구독
    private void Awake()
    {
        CacheComponents();
        SubscribeEvents();
    } //private void Awake()

    //활성 Trigger의 남은 지속시간 갱신 및 만료 처리
    private void Update()
    {
        if (m_ActiveTriggers.Count == 0)
            return;

        float deltaTime = Mathf.Max(0f, Time.deltaTime);

        for (int i = 0; i < m_ActiveTriggers.Count; i++)
        {
            ActiveTrigger trigger = m_ActiveTriggers[i];

            if (trigger == null)
            {
                m_ActiveTriggers.RemoveAt(i);
                i--;
                continue;
            }

            if (trigger.m_Definition == null)
            {
                QueueTriggerRemoval(trigger);
                continue;
            }

            if (trigger.m_IsPendingRemoval)
                continue;

            trigger.m_RemainingDuration -= deltaTime;

            if (trigger.m_RemainingDuration <= 0f)
                QueueTriggerRemoval(trigger);
        }

        RemoveBufferedTriggers();
    } //private void Update()

    //대상의 Root Object에서 Trigger Controller를 찾고 없으면 생성
    public static ActionTriggerController GetOrCreate(GameObject a_TargetObject)
    {
        if (a_TargetObject == null)
            return null;

        CombatTarget combatTarget = a_TargetObject.GetComponent<CombatTarget>();

        if (combatTarget == null)
            combatTarget = a_TargetObject.GetComponentInParent<CombatTarget>();

        GameObject rootObject =
            combatTarget != null && combatTarget.RootObject != null
                ? combatTarget.RootObject
                : a_TargetObject;

        ActionTriggerController controller =
            rootObject.GetComponent<ActionTriggerController>();

        if (controller == null)
            controller = rootObject.AddComponent<ActionTriggerController>();

        return controller;
    } //public static ActionTriggerController GetOrCreate()

    //새 Trigger를 등록하고 Runtime 상태 초기화
    public bool AddTrigger(ActionTriggerApplication a_Application)
    {
        ActionTriggerDefinition definition = a_Application.m_Definition;

        if (definition == null || string.IsNullOrWhiteSpace(definition.TriggerId))
            return false;

        CacheComponents();
        SubscribeEvents();

        if (HasTrigger(
                definition.TriggerId,
                a_Application.m_SourceObject))
        {
            return false;
        }

        ActiveTrigger trigger = new ActiveTrigger
        {
            m_Definition = definition,

            m_SourceObject = a_Application.m_SourceObject,
            m_SourceName = a_Application.m_SourceName,

            m_AssetProvider = a_Application.m_AssetProvider,
            m_ResourceHandle = a_Application.m_ResourceHandle,

            m_RemainingDuration = definition.Duration,
            m_RemainingTriggerCount = definition.TriggerCount,

            m_IsExecuting = false,
            m_IsPendingRemoval = false
        };

        m_ActiveTriggers.Add(trigger);

        return true;
    } //public bool AddTrigger()

    //동일 Trigger ID와 Source를 가진 Trigger가 등록되어 있는지 확인
    public bool HasTrigger(
        string a_TriggerId,
        GameObject a_SourceObject)
    {
        if (string.IsNullOrWhiteSpace(a_TriggerId))
            return false;

        for (int i = 0; i < m_ActiveTriggers.Count; i++)
        {
            ActiveTrigger trigger = m_ActiveTriggers[i];

            if (trigger?.m_Definition == null ||
                trigger.m_IsPendingRemoval)
            {
                continue;
            }

            if (trigger.m_Definition.TriggerId != a_TriggerId ||
                trigger.m_SourceObject != a_SourceObject)
            {
                continue;
            }

            return true;
        }

        return false;
    } //public bool HasTrigger()

    //동일 Trigger ID와 Source를 가진 Trigger 제거
    public bool RemoveTrigger(
        string a_TriggerId,
        GameObject a_SourceObject)
    {
        if (string.IsNullOrWhiteSpace(a_TriggerId))
            return false;

        for (int i = m_ActiveTriggers.Count - 1; i >= 0; i--)
        {
            ActiveTrigger trigger = m_ActiveTriggers[i];

            if (trigger == null)
            {
                if (m_EventProcessingDepth == 0)
                    m_ActiveTriggers.RemoveAt(i);

                continue;
            }

            if (trigger.m_Definition == null)
            {
                if (m_EventProcessingDepth > 0)
                    QueueTriggerRemoval(trigger);
                else
                    ReleaseTrigger(trigger, true);

                continue;
            }

            if (trigger.m_Definition.TriggerId != a_TriggerId ||
                trigger.m_SourceObject != a_SourceObject)
            {
                continue;
            }

            if (m_EventProcessingDepth > 0)
                QueueTriggerRemoval(trigger);
            else
                ReleaseTrigger(trigger, true);

            return true;
        }

        return false;
    } //public bool RemoveTrigger()

    //대상이 받은 Damage Event를 Trigger 처리로 전달
    private void OnDamageReceived(
        DamageInfo a_DamageInfo,
        float a_AppliedDamage)
    {
        GameObject targetObject = GetTriggerTargetObject();

        ProcessDamageTrigger(
            ActionTriggerType.DamageReceived,
            a_DamageInfo,
            a_AppliedDamage,
            targetObject
        );
    } //private void OnDamageReceived()

    //대상이 가한 Damage Event를 Trigger 처리로 전달
    private void OnDamageDealt(
        DamageInfo a_DamageInfo,
        float a_AppliedDamage,
        GameObject a_TargetObject)
    {
        ProcessDamageTrigger(
            ActionTriggerType.DamageDealt,
            a_DamageInfo,
            a_AppliedDamage,
            a_TargetObject
        );
    } //private void OnDamageDealt()

    //Damage Event와 일치하는 Trigger를 검사하고 Reaction 실행
    private void ProcessDamageTrigger(
        ActionTriggerType a_TriggerType,
        DamageInfo a_DamageInfo,
        float a_AppliedDamage,
        GameObject a_TargetObject)
    {
        if (m_ActiveTriggers.Count == 0)
            return;

        m_EventProcessingDepth++;

        try
        {
            int triggerCount = m_ActiveTriggers.Count;

            for (int i = 0; i < triggerCount; i++)
            {
                if (i >= m_ActiveTriggers.Count)
                    break;

                ActiveTrigger trigger = m_ActiveTriggers[i];

                if (CanTriggerFromDamage(
                        trigger,
                        a_TriggerType,
                        a_DamageInfo,
                        a_AppliedDamage) == false)
                {
                    continue;
                }

                ExecuteTrigger(
                    trigger,
                    a_DamageInfo.m_HitPoint,
                    a_TargetObject
                );
            }
        }
        finally
        {
            m_EventProcessingDepth--;

            if (m_EventProcessingDepth == 0)
                RemoveBufferedTriggers();
        }
    } //private void ProcessDamageTrigger()

    //현재 Trigger Controller가 관리하는 전투 대상의 Root Object 반환
    private GameObject GetTriggerTargetObject()
    {
        if (m_CombatTarget == null)
            CacheComponents();

        return m_CombatTarget != null && m_CombatTarget.RootObject != null
            ? m_CombatTarget.RootObject
            : gameObject;
    } //private GameObject GetTriggerTargetObject()

    //Damage Event가 Trigger Definition의 모든 발동 조건을 만족하는지 확인
    private bool CanTriggerFromDamage(
        ActiveTrigger a_Trigger,
        ActionTriggerType a_TriggerType,
        DamageInfo a_DamageInfo,
        float a_AppliedDamage)
    {
        if (a_Trigger?.m_Definition == null ||
            a_Trigger.m_IsExecuting ||
            a_Trigger.m_IsPendingRemoval)
        {
            return false;
        }

        ActionTriggerDefinition definition = a_Trigger.m_Definition;

        if (definition.TriggerType != a_TriggerType)
            return false;

        if (definition.RequirePositiveDamage && a_AppliedDamage <= 0f)
            return false;

        if (definition.FilterDamageType &&
            a_DamageInfo.m_DamageType != definition.RequiredDamageType)
        {
            return false;
        }

        if (definition.IgnoreOwnReaction &&
            a_DamageInfo.m_SkillKey == GetReactionSourceKey(a_Trigger))
        {
            return false;
        }

        return HasRequiredStatus(definition);
    } //private bool CanTriggerFromDamage()

    //Trigger 발동에 필요한 Status가 현재 대상에게 존재하는지 확인
    private bool HasRequiredStatus(ActionTriggerDefinition a_Definition)
    {
        if (a_Definition == null)
            return false;

        ActionStatusDefinition requiredStatus = a_Definition.RequiredStatus;

        if (requiredStatus == null)
            return true;

        if (m_StatusController == null)
            CacheComponents();

        if (m_StatusController == null)
            return false;

        return m_StatusController.HasStatus(requiredStatus);
    } //private bool HasRequiredStatus()

    //Trigger 소비 정책을 먼저 처리한 뒤 필요한 Status와 Reaction 실행
    private void ExecuteTrigger(
        ActiveTrigger a_Trigger,
        Vector2 a_ImpactPosition,
        GameObject a_HitObject)
    {
        if (a_Trigger?.m_Definition == null ||
            a_Trigger.m_IsPendingRemoval)
        {
            return;
        }

        a_Trigger.m_IsExecuting = true;

        try
        {
            ActionTriggerDefinition definition = a_Trigger.m_Definition;

            if (ShouldConsumeTrigger(a_Trigger))
                QueueTriggerRemoval(a_Trigger);

            if (definition.RemoveRequiredStatusOnTrigger)
                RemoveRequiredStatus(definition);

            ExecuteReactionAction(
                a_Trigger,
                a_ImpactPosition,
                a_HitObject
            );
        }
        finally
        {
            a_Trigger.m_IsExecuting = false;
        }
    } //private void ExecuteTrigger()

    //Consume Policy에 따라 Trigger의 남은 발동 횟수와 제거 여부 결정
    private bool ShouldConsumeTrigger(ActiveTrigger a_Trigger)
    {
        switch (a_Trigger.m_Definition.ConsumePolicy)
        {
            case ActionTriggerConsumePolicy.Never:
                return false;

            case ActionTriggerConsumePolicy.OnTrigger:
                return true;

            case ActionTriggerConsumePolicy.AfterTriggerCount:
                a_Trigger.m_RemainingTriggerCount--;
                return a_Trigger.m_RemainingTriggerCount <= 0;
        }

        return false;
    } //private bool ShouldConsumeTrigger()

    //Trigger 발동 시 요구 Status 제거
    private void RemoveRequiredStatus(ActionTriggerDefinition a_Definition)
    {
        if (a_Definition == null || a_Definition.RequiredStatus == null)
            return;

        if (m_StatusController == null)
            CacheComponents();

        if (m_StatusController == null)
            return;

        string statusId = a_Definition.RequiredStatus.StatusId;

        if (string.IsNullOrWhiteSpace(statusId))
            return;

        m_StatusController.RemoveStatus(statusId);
    } //private void RemoveRequiredStatus()

    //Trigger 발동 시 Reaction Action 실행
    private void ExecuteReactionAction(
        ActiveTrigger a_Trigger,
        Vector2 a_ImpactPosition,
        GameObject a_HitObject)
    {
        ActionDefinition reactionAction = a_Trigger.m_Definition.ReactionAction;

        if (reactionAction == null)
            return;

        GameObject targetObject = GetTriggerTargetObject();

        Transform targetTransform =
            m_CombatTarget != null && m_CombatTarget.RootTransform != null
                ? m_CombatTarget.RootTransform
                : transform;

        GameObject reactionHitObject = a_HitObject != null
            ? a_HitObject
            : targetObject;

        GameObject sourceObject = a_Trigger.m_SourceObject;

        Transform sourceTransform = sourceObject != null
            ? sourceObject.transform
            : targetTransform;

        Rigidbody2D sourceRigidbody = sourceObject != null
            ? sourceObject.GetComponent<Rigidbody2D>()
            : null;

        Vector2 useDirection =
            reactionHitObject != null && sourceTransform != null
                ? (Vector2)reactionHitObject.transform.position - (Vector2)sourceTransform.position
                : targetTransform != null && sourceTransform != null
                    ? (Vector2)targetTransform.position - (Vector2)sourceTransform.position
                    : Vector2.right;

        if (useDirection.sqrMagnitude <= 0.001f)
            useDirection = Vector2.right;
        else
            useDirection.Normalize();

        string sourceName = GetReactionSourceKey(a_Trigger);

        ActionUserContext context = new ActionUserContext(
            this,
            sourceObject,
            sourceTransform,
            sourceRigidbody,
            sourceObject != null ? sourceObject.GetComponent<Player>() : null,
            useDirection,
            a_ImpactPosition,
            reactionHitObject,
            sourceName,
            null,
            a_Trigger.m_AssetProvider
        );

        ItemUseResult result = reactionAction.Execute(context);

        if (result.m_IsSuccess == false)
        {
            Debug.LogWarning(
                $"[ActionTrigger] Reaction Action 실행 실패. " +
                $"Trigger={a_Trigger.m_Definition.TriggerId}, " +
                $"Action={reactionAction.name}, Message={result.m_Message}",
                this
            );
        }
    } //private void ExecuteReactionAction()

    //Reaction Damage의 재귀 발동을 식별할 고유 Source Key 반환
    private static string GetReactionSourceKey(ActiveTrigger a_Trigger)
    {
        if (a_Trigger?.m_Definition == null)
            return DefaultReactionSourceKey;

        return $"TriggerReaction_{a_Trigger.m_Definition.TriggerId}";
    } //private static string GetReactionSourceKey()

    //Trigger 제거를 Buffer에 등록하고 중복 등록 방지
    private void QueueTriggerRemoval(ActiveTrigger a_Trigger)
    {
        if (a_Trigger == null || a_Trigger.m_IsPendingRemoval)
            return;

        a_Trigger.m_IsPendingRemoval = true;
        m_RemoveBuffer.Add(a_Trigger);
    } //private void QueueTriggerRemoval()

    //Trigger 실행에 필요한 Event Hub, Status Controller와 CombatTarget 탐색
    private void CacheComponents()
    {
        if (m_CombatTarget == null)
        {
            m_CombatTarget = GetComponent<CombatTarget>();

            if (m_CombatTarget == null)
                m_CombatTarget = GetComponentInParent<CombatTarget>();
        }

        GameObject targetObject =
            m_CombatTarget != null && m_CombatTarget.RootObject != null
                ? m_CombatTarget.RootObject
                : gameObject;

        if (m_EventHub == null)
            m_EventHub = ActionTriggerEventHub.GetOrCreate(targetObject);

        if (m_StatusController == null)
            m_StatusController = targetObject.GetComponent<ActionStatusController>();
    } //private void CacheComponents()

    //Damage Event Hub에 Trigger 처리 Callback 등록
    private void SubscribeEvents()
    {
        if (m_EventHub == null)
            return;

        m_EventHub.DamageReceived -= OnDamageReceived;
        m_EventHub.DamageReceived += OnDamageReceived;

        m_EventHub.DamageDealt -= OnDamageDealt;
        m_EventHub.DamageDealt += OnDamageDealt;
    } //private void SubscribeEvents()

    //Trigger의 Resource Reservation을 정리하고 활성 목록에서 제거
    private void ReleaseTrigger(
        ActiveTrigger a_Trigger,
        bool a_StartRecovery)
    {
        if (a_Trigger == null)
            return;

        StackResourceReservationHandle resourceHandle =
            a_Trigger.m_ResourceHandle;

        a_Trigger.m_ResourceHandle = null;
        a_Trigger.m_IsPendingRemoval = false;

        if (resourceHandle != null && resourceHandle.IsValid)
        {
            if (a_StartRecovery)
                resourceHandle.StartRecovery();
            else
                resourceHandle.ReleaseImmediately();
        }

        m_ActiveTriggers.Remove(a_Trigger);
    } //private void ReleaseTrigger()

    //제거 예약된 Trigger와 연결 Resource 정리
    private void RemoveBufferedTriggers()
    {
        if (m_RemoveBuffer.Count == 0)
            return;

        for (int i = 0; i < m_RemoveBuffer.Count; i++)
        {
            ActiveTrigger trigger = m_RemoveBuffer[i];

            if (trigger == null)
                continue;

            ReleaseTrigger(
                trigger,
                true
            );
        }

        m_RemoveBuffer.Clear();
    } //private void RemoveBufferedTriggers()

    //비활성화 시 Event 구독과 모든 Trigger Resource 정리
    private void OnDisable()
    {
        if (m_EventHub != null)
        {
            m_EventHub.DamageReceived -= OnDamageReceived;
            m_EventHub.DamageDealt -= OnDamageDealt;
        }

        for (int i = m_ActiveTriggers.Count - 1; i >= 0; i--)
        {
            ActiveTrigger trigger = m_ActiveTriggers[i];

            if (trigger == null)
                continue;

            ReleaseTrigger(
                trigger,
                true
            );
        }

        m_ActiveTriggers.Clear();
        m_RemoveBuffer.Clear();

        m_EventProcessingDepth = 0;
    } //private void OnDisable()
} //public class ActionTriggerController