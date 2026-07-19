namespace System.Device.Location
{
    // Constants read out of the System.Device.dll shipped inside Pool Pro Online
    // 3, and corroborated by the titles that compare against them: Crackdown 2
    // and Kinectimals both test Permission against 2 for Denied.
    public enum GeoPositionPermission
    {
        Unknown = 0,
        Granted = 1,
        Denied = 2
    }
}
