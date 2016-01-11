module Avalanchain.Acl

open System

open FSharp.Core.Fluent
open Chessie.ErrorHandling
open Nessos.FsPickler
open Nessos.FsPickler.Json

open SecPrimitives
open SecKeys
open RefsAndPathes

type Role = Role of string
    with static member Admin = Role "Admin"

type Claim = Password of string // TODO: Add public/private keys based claims

type User = {
    UserName: string
    FullName: string
    Roles: Set<Role>  // Add Groups and OrgChart?
    Claims: Set<Claim>
    Disabled: bool
    Updated: DateTimeOffset
}

type StreamAcl = {
    ReadRoles: Set<Role>
    ReadInputEventsRoles: Set<Role> // Read Access to input events but not projected results 
    WriteRoles: Set<Role>
    DeleteRoles: Set<Role>
    ReadMetaRoles: Set<Role>
    WriteMetaRoles: Set<Role>
}
with 
    member inline private this.IsAllowed (user, roles) = roles |> Set.intersect user.Roles |> Set.isEmpty |> not // Replace with Linq for speed?
    member inline this.IsReadAllowed user = (user, this.ReadRoles) |> this.IsAllowed
    member inline this.IsReadInputEventsAllowed user = 
        ((user, this.ReadInputEventsRoles) |> this.IsAllowed) || (this.IsReadAllowed user)
    member inline this.IsWriteAllowed user = (user, this.WriteRoles) |> this.IsAllowed
    member inline this.IsDeleteAllowed user = (user, this.DeleteRoles) |> this.IsAllowed
    member inline this.IsReadMetaAllowed user = (user, this.ReadMetaRoles) |> this.IsAllowed
    member inline this.IsWriteMetaAllowed user = (user, this.WriteMetaRoles) |> this.IsAllowed

type StreamMetadata = {
    Acl: StreamAcl
}

type Permission =
    | ReadEvent
    | EmitEvent
    

