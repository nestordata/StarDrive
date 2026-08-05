#pragma once
#include <string>
#include <rpp/strview.h>
#include <rpp/file_io.h>

namespace SDNative
{
    using std::string;
    using std::vector;
    using rpp::strview;
    using rpp::load_buffer;
    ////////////////////////////////////////////////////////////////////////////////////

    struct ThrusterZone
    {
        float X, Y, Z, Scale;
    };

    static const strview Empty = "";

    struct ModuleSlotData
    {
        float PositionX;
        float PositionY;
        float Health;
        float ShieldPower;
        float Facing;
        strview InstalledModuleUID = Empty;
        strview HangarshipGuid     = Empty;
        strview State              = Empty;
        strview Restrictions       = Empty;
        strview SlotOptions        = Empty;
    };


    struct ShipData
    {
        strview Name              = Empty;
        strview Hull              = Empty;
        strview ShipStyle         = Empty;
        strview EventOnDeath      = Empty;
        strview SelectionGraphic  = Empty;
        strview IconPath          = Empty;
        strview ModelPath         = Empty;
        strview DefaultAIState    = Empty;
        strview Role              = "fighter";
        strview CombatState       = "AttackRuns";
        strview ShipCategory      = "Unclassified";
        strview HangarDesignation = "General";
		strview ModName           = Empty;
        int      TechScore             = 0;
        float    BaseStrength          = 0.0f;
        float    FixedUpkeep           = 0.0f;
        float    MechanicalBoardingDefense = 0.0f;
        unsigned char Experience       = 0;
        unsigned char Level            = 0;
        short    FixedCost             = 0;
        bool     Animated              = false;
        bool     HasFixedCost          = false;
        bool     HasFixedUpkeep        = false;
        bool     IsShipyard            = false;
        bool     CarrierShip           = false;
        bool     IsOrbitalDefense      = false;
        bool     HullUnlockable        = false;
        bool     UnLockable            = false;
        bool     AllModulesUnlockable  = true;

        // these expose raw pointers to C#, to make data conversion possible
        ThrusterZone* Thrusters     = nullptr;
        int ThrustersLen            = 0;
        ModuleSlotData* ModuleSlots = nullptr;
        int ModuleSlotsLen          = 0;
        strview* Techs              = nullptr;
        int TechsLen                = 0;

        strview ErrorMessage = Empty;

        // and this is our actual data storage, hidden from C#
        vector<ThrusterZone>   ThrusterList;
        vector<ModuleSlotData> ModuleSlotList;
        vector<strview>        TechsNeeded;
        string ErrorStr;
        load_buffer Data;

        bool LoadFromFile(const sd_wchar* filename);
        bool Error(string err);
    };

#ifndef SPATIAL_C_API
#  if defined(_MSC_VER)
#    define SPATIAL_C_API extern "C" __declspec(dllexport)
#  else
#    define SPATIAL_C_API extern "C" __attribute__((visibility("default")))
#  endif
#endif
#ifndef SPATIAL_CC
#  if defined(_MSC_VER)
#    define SPATIAL_CC __stdcall
#  else
#    define SPATIAL_CC
#  endif
#endif

    SPATIAL_C_API ShipData* SPATIAL_CC CreateShipDataParser(const sd_wchar* filename);
    SPATIAL_C_API void SPATIAL_CC DisposeShipDataParser(ShipData* data);


    ////////////////////////////////////////////////////////////////////////////////////
}

