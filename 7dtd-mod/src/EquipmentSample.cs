using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MC7DTD
{
    /// <summary>Called only from ModEvents.GameUpdate, never the network worker.</summary>
    public static class EquipmentSample
    {
        public static Dictionary<string,string> Read(EntityPlayerLocal player)
        {
            try {
                var eq=player.equipment;var inventory=player.inventory;
                if(eq==null || inventory==null || eq.GetSlotCount()<4)return null;
                var result=new Dictionary<string,string> {
                    ["head"]=Id(eq.GetSlotItem((int)EquipmentSlots.Head)),
                    ["body"]=Id(eq.GetSlotItem((int)EquipmentSlots.Chest)),
                    ["hands"]=Id(eq.GetSlotItem((int)EquipmentSlots.Hands)),
                    ["feet"]=Id(eq.GetSlotItem((int)EquipmentSlots.Feet))
                };
                var held=inventory.holdingItemStack;
                if(held==null || held.itemValue==null)return null;
                result["held_item"]=held.IsEmpty() || inventory.UsingBareHand() ? null : Id(held.itemValue);
                return result;
            } catch(Exception) { return null; } // Unknown sample, never fabricated empty slots.
        }
        static string Id(ItemValue value)
        {
            if(value==null || value.IsEmpty())return null;
            var name=value.ItemClass?.GetItemName();
            var id="7dtd:"+name;
            if(string.IsNullOrEmpty(name) || id.Length>128 || !Regex.IsMatch(id,@"\A7dtd:[A-Za-z0-9_.-]+\z"))throw new InvalidOperationException("Equipment item unavailable");
            return id;
        }
    }
}
