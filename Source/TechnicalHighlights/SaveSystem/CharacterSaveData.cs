using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterSaveData
{
    /*
    하나의 Character에 귀속되는 영구 진행 데이터를 저장하며
    현재는 장착된 주요 Skill Loadout을 Skill Key 기준으로 보관
    */

    [SerializeField] private CharicType m_CharicType = CharicType.Count;
    [SerializeField] private List<string> m_EquippedMajorSkillKeys = new List<string>();

    public CharicType CharicType
    {
        get => m_CharicType;
        internal set => m_CharicType = value;
    }

    public IReadOnlyList<string> EquippedMajorSkillKeys => m_EquippedMajorSkillKeys;

    internal List<string> MutableEquippedMajorSkillKeys => m_EquippedMajorSkillKeys;
} //public class CharacterSaveData