
using System.Collections.Generic;
using Catan3.Models;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
namespace Catan3.Utility
{
    public static class CatanFont
    {
        public static readonly string City = "\uE900";
        public static readonly string Deserter = "\uE901";
        public static readonly string Diplomat = "\uE902";
        public static readonly string Gate = "\uE903";
        public static readonly string Politics = "\uE904";
        public static readonly string Spy = "\uE905";
        public static readonly string Inventor = "\uE906";
        public static readonly string Laurel = "\uE907";
        public static readonly string Merchant = "\uE908";
        public static readonly string NoEntitlement = "\uE90A";
        public static readonly string Science = "\uE901B";
        public static readonly string Pirate = "\uE90C";
        public static readonly string Ship = "\uE90D";
        public static readonly string SolidShield = "\uE925";
        public static readonly string Settlement = "\uE926";
        public static readonly string FancyShield = "\uE927";
        public static readonly string Soldier = "\uE90E";
        public static readonly string Knight = "\uE930";
        public static readonly string Road = "\uE909";
        public static readonly string Metro = "\uE90F";
        public static readonly string LargestArmy = "\uE90E";
        public static readonly string LongestRoad = "\uE915";
        public static readonly string Score = "\uE907";
        public static readonly string Target = "\uE916";
        public static readonly string Sum = "\uE910";
        public static readonly string BadRoll = "\uE913";
        public static readonly string GoodRoll = "\uE914";
        public static readonly string Star = "\uE911";
        public static readonly Dictionary<Entitlement, string> EntitlementGlyph = new()
        {
            {Entitlement.Settlement, Settlement },
            {Entitlement.City, City },
            {Entitlement.Soldier, Pirate },
            {Entitlement.Road, Road }
        };
        
    }
}
