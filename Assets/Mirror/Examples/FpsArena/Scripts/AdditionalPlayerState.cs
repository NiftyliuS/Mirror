using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.FpsArena.Scripts
{
    public struct AdditionalNetworkPlayerState
    {
        public byte Health;
        public byte ActiveWeapon;
        public byte SinceFire;
        public byte SinceReload;
        public byte PistolAmmo;
        public byte RifleAmmo;
        public byte ShotgunAmmo;
        public byte SniperAmmo;
    }

    public static class AdditionalNetworkPlayerStateSerializer
    {
        public static void WriteNetworkPlayerState(this NetworkWriter writer, AdditionalNetworkPlayerState value)
        {
            // Write the bytes in order to send efficiently
            writer.WriteByte(value.Health);
            writer.WriteByte(value.ActiveWeapon);
            writer.WriteByte(value.SinceFire);
            writer.WriteByte(value.SinceReload);
            writer.WriteByte(value.PistolAmmo);
            writer.WriteByte(value.RifleAmmo);
            writer.WriteByte(value.ShotgunAmmo);
            writer.WriteByte(value.SniperAmmo);
        }

        public static AdditionalNetworkPlayerState ReadNetworkPlayerState(this NetworkReader reader)
        {
            // Read the bytes in order to restore the state
            var health = reader.ReadByte();
            var activeWeapon = reader.ReadByte();
            var sinceFire = reader.ReadByte();
            var sinceReload = reader.ReadByte();
            var pistolAmmo = reader.ReadByte();
            var rifleAmmo = reader.ReadByte();
            var shotgunAmmo = reader.ReadByte();
            var sniperAmmo = reader.ReadByte();

            return new AdditionalNetworkPlayerState()
            {
                Health = health,
                ActiveWeapon = activeWeapon,
                SinceFire = sinceFire,
                SinceReload = sinceReload,
                PistolAmmo = pistolAmmo,
                RifleAmmo = rifleAmmo,
                ShotgunAmmo = shotgunAmmo,
                SniperAmmo = sniperAmmo,
            };
        }
    }
}
