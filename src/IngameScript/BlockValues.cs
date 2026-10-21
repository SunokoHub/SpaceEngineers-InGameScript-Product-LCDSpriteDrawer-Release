using System;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    #region mdk preserve
    public enum BlockValueKind
    {
        Speed, Enabled, IsWorking, IsFunctional, PowerOutput, PowerMaxOutput,
        BatteryStoredPower, BatteryMaxStoredPower, BatteryChargeRatio, BatteryInput, BatteryMaxInput, BatteryIsCharging,
        GasFillRatio, GasCapacity,
        DoorOpenRatio, PistonPosition, PistonVelocity, RotorAngle, RotorVelocity, RotorLocked,
        ShipBaseMass, ShipTotalMass, ShipPhysicalMass, NaturalGravity, ArtificialGravity, TotalGravity, AngularSpeed,
        IsUnderControl, HandBrake, Dampeners
    }
    #endregion

    public static class BlockValues
    {
        public static bool TryKind(string text, out BlockValueKind kind) =>
            Enum.TryParse(text, out kind) && kind >= BlockValueKind.Speed && kind <= BlockValueKind.Dampeners && kind.ToString() == text;

        public static bool IsBoolean(BlockValueKind kind) => kind == BlockValueKind.Enabled || kind == BlockValueKind.IsWorking ||
            kind == BlockValueKind.IsFunctional || kind == BlockValueKind.BatteryIsCharging || kind == BlockValueKind.RotorLocked ||
            kind == BlockValueKind.IsUnderControl || kind == BlockValueKind.HandBrake || kind == BlockValueKind.Dampeners;

        public static string Key(BlockValueKind kind, string name) => kind + ":" + name;

        public static bool Accepts(IMyTerminalBlock block, BlockValueKind kind)
        {
            if (block == null) return false;
            switch (kind)
            {
                case BlockValueKind.Enabled: return block is IMyFunctionalBlock;
                case BlockValueKind.IsWorking: case BlockValueKind.IsFunctional: return true;
                case BlockValueKind.PowerOutput: case BlockValueKind.PowerMaxOutput: return block is IMyPowerProducer;
                case BlockValueKind.BatteryStoredPower: case BlockValueKind.BatteryMaxStoredPower:
                case BlockValueKind.BatteryChargeRatio: case BlockValueKind.BatteryInput:
                case BlockValueKind.BatteryMaxInput: case BlockValueKind.BatteryIsCharging: return block is IMyBatteryBlock;
                case BlockValueKind.GasFillRatio: case BlockValueKind.GasCapacity: return block is IMyGasTank;
                case BlockValueKind.DoorOpenRatio: return block is IMyDoor;
                case BlockValueKind.PistonPosition: case BlockValueKind.PistonVelocity: return block is IMyPistonBase;
                case BlockValueKind.RotorAngle: case BlockValueKind.RotorVelocity: case BlockValueKind.RotorLocked: return block is IMyMotorStator;
                case BlockValueKind.Speed: case BlockValueKind.ShipBaseMass: case BlockValueKind.ShipTotalMass:
                case BlockValueKind.ShipPhysicalMass: case BlockValueKind.NaturalGravity: case BlockValueKind.ArtificialGravity:
                case BlockValueKind.TotalGravity: case BlockValueKind.AngularSpeed: case BlockValueKind.IsUnderControl:
                case BlockValueKind.HandBrake: case BlockValueKind.Dampeners: return block is IMyShipController;
                default: return false;
            }
        }

        public static bool TryRatio(double current, double maximum, out double value)
        {
            value = 0;
            if (!Motion.Finite(current) || !Motion.Finite(maximum) || maximum <= 0) return false;
            value = current / maximum;
            return Motion.Finite(value);
        }

        public static bool TryRead(IMyTerminalBlock block, BlockValueKind kind,
            out double value, out bool flag, out string reason)
        {
            value = 0; flag = false; reason = "";
            if (!Accepts(block, kind)) return Reject("Invalid block value source.", out reason);
            switch (kind)
            {
                case BlockValueKind.Enabled: flag = ((IMyFunctionalBlock)block).Enabled; break;
                case BlockValueKind.IsWorking: flag = block.IsWorking; break;
                case BlockValueKind.IsFunctional: flag = block.IsFunctional; break;
                case BlockValueKind.PowerOutput: value = ((IMyPowerProducer)block).CurrentOutput; break;
                case BlockValueKind.PowerMaxOutput: value = ((IMyPowerProducer)block).MaxOutput; break;
                case BlockValueKind.BatteryStoredPower: value = ((IMyBatteryBlock)block).CurrentStoredPower; break;
                case BlockValueKind.BatteryMaxStoredPower: value = ((IMyBatteryBlock)block).MaxStoredPower; break;
                case BlockValueKind.BatteryChargeRatio:
                    var battery = (IMyBatteryBlock)block;
                    if (!TryRatio(battery.CurrentStoredPower, battery.MaxStoredPower, out value))
                        return Reject("Invalid battery charge ratio.", out reason);
                    break;
                case BlockValueKind.BatteryInput: value = ((IMyBatteryBlock)block).CurrentInput; break;
                case BlockValueKind.BatteryMaxInput: value = ((IMyBatteryBlock)block).MaxInput; break;
                case BlockValueKind.BatteryIsCharging: flag = ((IMyBatteryBlock)block).IsCharging; break;
                case BlockValueKind.GasFillRatio: value = ((IMyGasTank)block).FilledRatio; break;
                case BlockValueKind.GasCapacity: value = ((IMyGasTank)block).Capacity; break;
                case BlockValueKind.DoorOpenRatio: value = ((IMyDoor)block).OpenRatio; break;
                case BlockValueKind.PistonPosition: value = ((IMyPistonBase)block).CurrentPosition; break;
                case BlockValueKind.PistonVelocity: value = ((IMyPistonBase)block).Velocity; break;
                case BlockValueKind.RotorAngle: value = ((IMyMotorStator)block).Angle; break;
                case BlockValueKind.RotorVelocity: value = ((IMyMotorStator)block).TargetVelocityRPM; break;
                case BlockValueKind.RotorLocked: flag = ((IMyMotorStator)block).RotorLock; break;
                case BlockValueKind.Speed: value = ((IMyShipController)block).GetShipSpeed(); break;
                case BlockValueKind.ShipBaseMass: value = ((IMyShipController)block).CalculateShipMass().BaseMass; break;
                case BlockValueKind.ShipTotalMass: value = ((IMyShipController)block).CalculateShipMass().TotalMass; break;
                case BlockValueKind.ShipPhysicalMass: value = ((IMyShipController)block).CalculateShipMass().PhysicalMass; break;
                case BlockValueKind.NaturalGravity: value = ((IMyShipController)block).GetNaturalGravity().Length(); break;
                case BlockValueKind.ArtificialGravity: value = ((IMyShipController)block).GetArtificialGravity().Length(); break;
                case BlockValueKind.TotalGravity: value = ((IMyShipController)block).GetTotalGravity().Length(); break;
                case BlockValueKind.AngularSpeed: value = ((IMyShipController)block).GetShipVelocities().AngularVelocity.Length(); break;
                case BlockValueKind.IsUnderControl: flag = ((IMyShipController)block).IsUnderControl; break;
                case BlockValueKind.HandBrake: flag = ((IMyShipController)block).HandBrake; break;
                case BlockValueKind.Dampeners: flag = ((IMyShipController)block).DampenersOverride; break;
            }
            return Motion.Finite(value) || Reject("Block value is not finite.", out reason);
        }

        static bool Reject(string message, out string reason) { reason = message; return false; }
    }
}
