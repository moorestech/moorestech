namespace DeadMemberAudit.Model;

// 参照は実在するが公開範囲が広すぎるメンバーの、縮小先アクセシビリティ
// The accessibility a referenced-but-over-exposed member could be narrowed to
public enum OverPublicScope
{
    None,
    Private,
    Internal,
}
