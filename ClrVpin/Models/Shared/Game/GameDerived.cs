﻿using System;
using PropertyChanged;
// ReSharper disable MemberCanBePrivate.Global - public setters required to support json deserialization, refer DatabaseItem

namespace ClrVpin.Models.Shared.Game
{
    [AddINotifyPropertyChangedInterface]
    public class GameDerived
    {
        public int Number { get; set; }
        public string Ipdb { get; set; }
        public string IpdbUrl { get; set; }
        public string NameLowerCase { get; set; }
        public string DescriptionLowerCase { get; set; }
        public bool IsOriginal { get; set; }
        public string TableFileWithExtension { get; set; }

        public static void Init(GameDetail gameDetail, int? number = null)
        {
            var derived = gameDetail.Derived;

            derived.Number = number ?? derived.Number;

            derived.IsOriginal = CheckIsOriginal(gameDetail.Game.Manufacturer, gameDetail.Game.Name);

            if (derived.IsOriginal)
            {
                derived.Ipdb = null;
                derived.IpdbUrl = null;
                //derived.IpdbNr = null;

                // don't assign null as this will result in the tag being removed from serialization.. which is valid, but inconsistent with the original xml file that always defines <ipdbid>
                //derived.IpdbId = "";
            }
            else
            {
                derived.Ipdb = gameDetail.Game.IpdbId ?? gameDetail.Game.IpdbNr ?? derived.Ipdb;
                derived.IpdbUrl = derived.Ipdb == null ? null : $"https://www.ipdb.org/machine.cgi?id={derived.Ipdb}";
            }

            // memory optimisation to perform this operation once on database read instead of multiple times during fuzzy comparison (refer Fuzzy.GetUniqueMatch)
            derived.NameLowerCase = gameDetail.Game.Name.ToLower();
            derived.DescriptionLowerCase = gameDetail.Game.Description.ToLower();

            derived.TableFileWithExtension = gameDetail.Game.Name + ".vpx";
        }

        // assign isOriginal based on manufacturer
        public static bool CheckIsOriginal(string manufacturer, string name)
        {
            var isManufacturerOriginal = manufacturer?.StartsWith("Original", StringComparison.InvariantCultureIgnoreCase) == true ||
                   manufacturer?.StartsWith("OrbitalPin", StringComparison.InvariantCultureIgnoreCase) == true ||
                   manufacturer?.StartsWith("HorsePin", StringComparison.InvariantCultureIgnoreCase) == true ||
                   manufacturer?.StartsWith("Zen Studios", StringComparison.InvariantCultureIgnoreCase) == true ||
                   manufacturer?.StartsWith("Professional Pinball", StringComparison.InvariantCultureIgnoreCase) == true ||
                   manufacturer?.StartsWith("Dream Pinball 3D", StringComparison.InvariantCultureIgnoreCase) == true;
            
            var isNameOriginal =  name?.Equals("Jurassic park - Limited Edition", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Kiss Live", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Dream Pinball 3D", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Sharpshooter", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Silver Line", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Space Cadet", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Yamanobori", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Siggi's Spider-Man Classic", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Mad Scientist", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Midnight Magic", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Pro Pinball The Web", StringComparison.InvariantCultureIgnoreCase) == true || 
                                  name?.Equals("Octopus", StringComparison.InvariantCultureIgnoreCase) == true ;

            return isManufacturerOriginal || isNameOriginal;
        }
    }
}