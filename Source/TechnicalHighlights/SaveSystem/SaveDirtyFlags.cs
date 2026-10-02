using System;

[Flags]
public enum SaveDirtyFlags
{
    //저장할 변경 사항이 없는 상태
    None = 0,

    //Gold 재화가 변경된 상태
    Gold = 1 << 0,

    //Character의 주요 Skill Loadout이 변경된 상태
    SkillLoadout = 1 << 1,

    //마지막 선택 Character가 변경된 상태
    CharacterSelection = 1 << 2
} //public enum SaveDirtyFlags