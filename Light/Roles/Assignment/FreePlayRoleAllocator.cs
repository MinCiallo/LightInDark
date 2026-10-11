using System.Collections.Generic;
using System.Linq;
using LightInDark.Events;
using LightInDark.Roles;
using LightInDark.Roles.Assignment;
using Light.Roles.Crewmates;

namespace Light.Roles.Assignment;

public sealed class FreePlayRoleAllocator : IRoleAllocator
{
    public void Assign(List<byte> impostors, List<byte> others)
    {
        var table = new RoleTable();
        foreach (byte id in impostors.Concat(others).Distinct())
            table.SetRole(id, RolePinManager.TryGetPin(id, out var pin) ? pin : Crewmate.MyRole);
        EventTriggers.OnPreFixAssignment(table);
        table.Determine();
    }
}
