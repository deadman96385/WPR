// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Class: GeoPositionStatus
**
** Purpose: Represents a GeoPositionStatus object
**
=============================================================================*/

namespace System.Device.Location
{
    // The constants are the phone's, not this file's declaration order. They are
    // read out of the System.Device.dll that Pool Pro Online 3 ships, and titles
    // switch on them directly: Crackdown 2 branches value 0 into its "you have
    // disabled the Location Service" path and value 1 into "GPS Ready".
    public enum GeoPositionStatus
    {
        Disabled = 0,       // Location service disabled or access denied
        Ready = 1,          // Enabled
        Initializing = 2,   // Working to acquire data
        NoData = 3          // We have access to sensors, but we cannnot resolve
    }
}
