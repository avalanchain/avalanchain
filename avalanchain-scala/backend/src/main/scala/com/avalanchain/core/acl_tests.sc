import com.avalanchain.core.roles.PermissionType.{Allow, Deny}
import com.avalanchain.core.roles.{ACL, Permission}

val a =

val acl_r1 = ACL(Map(Permission("read") -> Allow))
val acl_r2 = ACL(Map(Permission("read") -> Deny))
val acl_w1 = ACL(Map(Permission("write") -> Allow))
val acl_w2 = ACL(Map(Permission("write") -> Deny))
val acl_r1_r2 = acl_r1 + acl_r2
val acl_r2_r1 = acl_r2 + acl_r1
val acl_r1_r1 = acl_r1 + acl_r1
val acl_r2_r2 = acl_r2 + acl_r2

val acl_r1_w1 = acl_r1 + acl_w2
val acl_r2_w2 = acl_r2 + acl_w1
val acl_r1_w2 = acl_r1 + acl_w2
val acl_r2_w1 = acl_r2 + acl_w1

val acl_r1_w1_w1 = acl_r1_w1 + acl_w1
val acl_r2_w2_w1 = acl_r2_w1 + acl_w1
val acl_r1_w2_w1 = acl_r1_w2 + acl_w1
val acl_r2_w1_w1 = acl_r2_w2 + acl_w1

val acl_r1_w1_w2 = acl_r1_w1 + acl_w2
val acl_r2_w2_w2 = acl_r2_w1 + acl_w2
val acl_r1_w2_w2 = acl_r1_w2 + acl_w2
val acl_r2_w1_w2 = acl_r2_w2 + acl_w2
