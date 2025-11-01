using Egg.Wire.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if !DB
using Newtonsoft.Json;
#endif

/*
 * HISTORY LESSON
 * ------- ------
 *
 * So you maybe wondering why this whole ThingState concept is so weird. Why not use simple enums to represent ThingState values?
 *
 * Previously, we did have ThingStates as enums. However these were used as "Flag" enums
 * (https://stackoverflow.com/questions/8447/what-does-the-flags-enum-attribute-mean-in-c)
 * where real-world states were represented as powers of 2, that can then be combined to create
 * composite states, such as InWarehouse = State1 | State2 | ...
 *
 * The backing type of this enum was a long (64-bit integer).
 *
 * This came in handy for many use-cases, however the maximum number of discrete values in a Flag enum
 * is (# of bits of data type - 1) (-1 to eliminate negative values), so for longs, this meant 63 discrete values.
 *
 * At the point of conception, we had made 15 states, and the possibly of exceeding this count seemed like a joke
 * ("63 states ought to be enough for anyone")
 *
 * However as our systems evolved, the number of discrete states being modeled increased, eventually coming very close
 * to the limit. That lead to this refactor.
 *
 * Internally, within the database, we'll continue to represent states as a long, but we'll drop the "flag" nature of this
 * value. However, we emulate this nature by representing ThingStates as a C# class; discrete values are represented by
 * SimpleThingState, and combined states as CompositeThingStates.
 *
 * We then introduce an explicit cast operator so SimpleThingState and longs can seamlessly cast between each other. While we
 * no longer have enums, this dual approach leads to some advantages, primarily, that we can't accidentally assign a composite
 * state to the persisted thing-state value; this was a possibility in the old system. At the same time, we maintain the ability
 * to have composite states. Implicit cast operators were considered, however they caused problems with LINQ-to-Entities expressions
 * creating unexpected run-time errors, as implicit casting isn't supported with Linq-to-Entities.
 *
 * We declare all possible states within the ThingState class as public static readonly values, and then use reflection to get
 * the state names for cases where we need to convert the thing state value to a string for display purposes, and serialization.
 *
 * */

namespace Egg.Shared.Domain.Inventory
{
#if !DB
    [JsonConverter(typeof(ThingStateJsonConverter))]
#endif
    public abstract class ThingState : IEquatable<ThingState>
    {
        public abstract SimpleThingState[] ToSimpleThingStates();

        public abstract long[] ToSimpleThingStateValues();

        public bool Equals(ThingState other)
        {
            if (this is SimpleThingState one && other is SimpleThingState two)
                return one.Equals(two);
            else if (this is CompositeThingState cone && other is CompositeThingState ctwo)
                return cone.Equals(ctwo);
            else
                return false;
        }

        public abstract override bool Equals(object other);

        public abstract override int GetHashCode();

        private static bool AreBothNull(ThingState thisState, ThingState otherState) => thisState is null && otherState is null;

        private static bool IsEitherNull(ThingState thisState, ThingState otherState) => thisState is null || otherState is null;

        public static bool operator ==(ThingState thisState, ThingState otherState)
            => AreBothNull(thisState, otherState) || (!IsEitherNull(thisState, otherState) && thisState.Equals(otherState));

        public static bool operator !=(ThingState thisState, ThingState otherState)
            => !(AreBothNull(thisState, otherState) || (!IsEitherNull(thisState, otherState) && thisState.Equals(otherState)));

        public static bool operator ==(ThingState thisState, CompositeThingState otherState)
            => AreBothNull(thisState, otherState) || (!IsEitherNull(thisState, otherState) && thisState.Equals(otherState));

        public static bool operator !=(ThingState thisState, CompositeThingState otherState)
            => !(AreBothNull(thisState, otherState) || (!IsEitherNull(thisState, otherState) && thisState.Equals(otherState)));

        public static bool operator ==(ThingState thisState, SimpleThingState otherState)
            => AreBothNull(thisState, otherState) || (!IsEitherNull(thisState, otherState) && thisState.Equals(otherState));

        public static bool operator !=(ThingState thisState, SimpleThingState otherState)
            => !(AreBothNull(thisState, otherState) || (!IsEitherNull(thisState, otherState) && thisState.Equals(otherState)));

        public SimpleThingState ToSimpleStateOrFail()
        {
            if (this is SimpleThingState sstate)
                return sstate;
            else
                throw new InvalidOperationException($"{this} is not a simple thing state");
        }

        public long ToSimpleStateValueOrFail() => ToSimpleStateOrFail().Value;

        public static ThingState operator |(ThingState a, ThingState b)
        {
            return new CompositeThingState(a, b);
        }

        // For access from F# unit tests
        public static ThingState Combine(ThingState a, ThingState b) => a | b;

        // NOTE: the order of definitions here is important
        // This will fail, with a null reference exception, as SimpleState2 is defined after SomeCompositeState
        // SimpleState SimpleState1 = ...
        // ThingState SomeCompositeState = SimpleState1 | SimpleState2
        // SimpleState SimpleState2 = ...
        // So to be safe, let's define all the Simple states first
        // However, you must exercise care when ordering composite states, or the service will crash on startup


        public static readonly SimpleThingState InPrimordialSoup = new SimpleThingState(0); // WAS 1L << 19,

        // InWarehouse/Unassigned sub-states
        public static readonly SimpleThingState InIncomingZone = new SimpleThingState(1L);
        public static readonly SimpleThingState InIncomingHoldZone = new SimpleThingState(1L << 51);
        public static readonly SimpleThingState InPreSortingZone = new SimpleThingState(1L << 37);
        public static readonly SimpleThingState InIntakeProcessingZone = new SimpleThingState(1L << 1);
        public static readonly SimpleThingState InTransferZone = new SimpleThingState(1L << 2);
        public static readonly SimpleThingState InReturnReshelvingZone = new SimpleThingState(1L << 38);
        public static readonly SimpleThingState InTransmutationZone = new SimpleThingState(1L << 52);
        public static readonly SimpleThingState InJunkyard = new SimpleThingState(1L << 3);
        public static readonly SimpleThingState InNarnia = new SimpleThingState(1L << 4);
        public static readonly SimpleThingState InGraveyard = new SimpleThingState(1L << 5);
        public static readonly SimpleThingState InRecycleTransit = new SimpleThingState(1L << 6);
        public static readonly SimpleThingState InRecycleZone = new SimpleThingState(1L << 7);
        public static readonly SimpleThingState InRecallTransit = new SimpleThingState(1L << 36);
        public static readonly SimpleThingState AvailableInShelf = new SimpleThingState(1L << 8);
        public static readonly SimpleThingState ExcessAvailableInShelf = new SimpleThingState(1L << 35);
        public static readonly SimpleThingState FlaggedMissingInShelf = new SimpleThingState(1L << 9);
        public static readonly SimpleThingState FlaggedDamagedInShelf = new SimpleThingState(1L << 10);
        public static readonly SimpleThingState FlaggedExpiredInShelf = new SimpleThingState(1L << 11);
        public static readonly SimpleThingState UninspectedInVendorReturnZone = new SimpleThingState(1L << 25);
        public static readonly SimpleThingState InReturnZoneNeverAssigned = new SimpleThingState(1L << 26);
        public static readonly SimpleThingState InReturnZoneCancelledWhilePicking = new SimpleThingState(1L << 27);
        public static readonly SimpleThingState InReturnZoneCancelledBeforeDispatch = new SimpleThingState(1L << 49);
        public static readonly SimpleThingState InReturnZoneReturnedFromDelivery = new SimpleThingState(1L << 50);
        public static readonly SimpleThingState PendingReturnInVendorReturnZone = new SimpleThingState(1L << 30);
        public static readonly SimpleThingState InReturnTransit = new SimpleThingState(1L << 33);
        public static readonly SimpleThingState ReportedMissingWithCustomer = new SimpleThingState(1L << 34);
        public static readonly SimpleThingState TransmutedOut = new SimpleThingState(1L << 53);
        public static readonly SimpleThingState InTransferZoneWithShelfAssignment = new SimpleThingState(1L << 40);
        public static readonly SimpleThingState InReturnReshelvingZoneWithShelfAssignment = new SimpleThingState(1L << 41);
        public static readonly SimpleThingState InTransferZoneWithNoShelfAssigned = new SimpleThingState(1L << 42);    // NoShelfAssigned is for frozen and frozen perishable where a manual shelf selection is required at shelving time.
        public static readonly SimpleThingState InReturnReshelvingZoneWithNoShelfAssigned = new SimpleThingState(1L << 43);
        public static readonly SimpleThingState InTransferZoneWithNoShelfSpaceFound = new SimpleThingState(1L << 44);
        public static readonly SimpleThingState InTransferZoneWithNoStaticShelfAssignment = new SimpleThingState(1L << 45);
        public static readonly SimpleThingState InReturnReshelvingZoneWithNoShelfSpaceFound = new SimpleThingState(1L << 46);
        public static readonly SimpleThingState InReturnReshelvingZoneWithNoStaticShelfAssignment = new SimpleThingState(1L << 47);
        public static readonly SimpleThingState HeldInShelfForTransitionToDifferentShipment = new SimpleThingState(1L << 48);  // transitionary state); should only happen inbetween a thing transitioning from helfInShelfForLocal for one shipment to heldInShelfForLocal another shipment  );
        public static readonly SimpleThingState InReturnZoneCancelledAfterImport = new SimpleThingState(1L << 58);
        public static readonly SimpleThingState InPerishableTransit = new SimpleThingState(1L << 62);

        // HeldInShelf sub-states
        public static readonly SimpleThingState HeldInShelfForExport = new SimpleThingState(1L << 12);
        public static readonly SimpleThingState HeldInShelfForLocal = new SimpleThingState(1L << 13);

        // InPurchaseZone sub-states
        public static readonly SimpleThingState InPurchaseZoneForLocal = new SimpleThingState(1L << 16);
        [Obsolete] public static readonly SimpleThingState InPurchaseZoneForExport = new SimpleThingState(1L << 24); //DEPRECATED: In Export Warehouse V2 Implementation
        public static readonly SimpleThingState InPurchaseZoneUnassigned = new SimpleThingState(1L << 28);

        // InWarehouse/AssignedToShipment sub-states
        public static readonly SimpleThingState InBasket = new SimpleThingState(1L << 14);
        public static readonly SimpleThingState InReadyBasket = new SimpleThingState(1L << 39);
        [Obsolete] public static readonly SimpleThingState InExportBasket = new SimpleThingState(1L << 15); //DEPRECATED: In Export Warehouse V2 Implementation
        public static readonly SimpleThingState InTransportAssigned = new SimpleThingState(1L << 17);
        public static readonly SimpleThingState InTransportPreviouslyAssigned = new SimpleThingState(1L << 32);


        // InWarehouse/MaybeAssignedToShipment sub-states
        // 1L << 54, 55, 56, 57, 59, 60, 61 Available
        //public static readonly SimpleThingState HeldInImportOverflowZone = new SimpleThingState(1L << 56);
        //public static readonly SimpleThingState AvailableInImportOverflowZone = new SimpleThingState(1L << 57);
        //public static readonly SimpleThingState InImportBasket = new SimpleThingState(1L << 61);
        //public static readonly SimpleThingState InImportBasketWithShelfAssignment = new SimpleThingState(1L << 54);
        //public static readonly SimpleThingState InImportBasketWithNoShelfSpaceFound = new SimpleThingState(1L << 55);
        //public static readonly SimpleThingState InImportBasketWithNoStaticShelfAssignment = new SimpleThingState(1L << 59);
        //public static readonly SimpleThingState InImportBasketWithNoShelfAssigned = new SimpleThingState(1L << 60);

        // PendingMarketPurchase substates
        public static readonly SimpleThingState PendingMarketPurchaseForLocal = new SimpleThingState(1L << 18);
        [Obsolete] public static readonly SimpleThingState PendingMarketPurchaseForExport = new SimpleThingState(1L << 23); //DEPRECATED: In Export Warehouse V2 Implementation
        public static readonly SimpleThingState PendingMarketPurchaseForDeficit = new SimpleThingState(1L << 29);

        // 19 is now available
        public static readonly SimpleThingState WithCustomer = new SimpleThingState(1L << 20);

        public static readonly SimpleThingState FailedMarketPurchase = new SimpleThingState(1L << 21);
        public static readonly SimpleThingState CancelledMarketPurchase = new SimpleThingState(1L << 22);

        public static readonly SimpleThingState SentBackToVendor = new SimpleThingState(1L << 31);

        //Export Warehouse
        public static readonly SimpleThingState ReservedInShelfForExport = new SimpleThingState(3);
        //|__Carton Transfer Zone
        public static readonly SimpleThingState InCartonTransferZone = new SimpleThingState(5);
        public static readonly SimpleThingState InCartonTransferZoneWithShelfAssignment = new SimpleThingState(6);
        public static readonly SimpleThingState InCartonTransferZoneWithNoShelfSpaceFound = new SimpleThingState(7);
        public static readonly SimpleThingState InCartonTransferZoneWithNoStaticShelfAssignment = new SimpleThingState(9);
        public static readonly SimpleThingState InCartonTransferZoneWithNoShelfAssigned = new SimpleThingState(10);
        //|__Shelved Carton
        public static readonly SimpleThingState AvailableInCartonForExport = new SimpleThingState(11);
        public static readonly SimpleThingState HeldInCartonForExport = new SimpleThingState(12);
        public static readonly SimpleThingState ReservedInCartonForExport = new SimpleThingState(13);
        //|__Unboxing Zone
        public static readonly SimpleThingState AvailableInUnboxingZoneForExport = new SimpleThingState(14);
        public static readonly SimpleThingState HeldInUnboxingZoneForExport = new SimpleThingState(15);
        public static readonly SimpleThingState ReservedInUnboxingZoneForExport = new SimpleThingState(17);
        //|__Dispatch Zone
        public static readonly SimpleThingState HeldInExportWarehouseDispatchZone = new SimpleThingState(18);
        public static readonly SimpleThingState ReservedInExportWarehouseDispatchZone = new SimpleThingState(19);

        //Dispatch Warehouse Import
        //|__Held Imported En Route To Shelf
        public static readonly SimpleThingState HeldInImportBasket = new SimpleThingState(20);
        public static readonly SimpleThingState HeldInImportBasketWithShelfAssignment = new SimpleThingState(21);
        public static readonly SimpleThingState HeldInImportBasketWithNoShelfSpaceFound = new SimpleThingState(22);
        public static readonly SimpleThingState HeldInImportBasketWithNoStaticShelfAssignment = new SimpleThingState(23);
        public static readonly SimpleThingState HeldInImportBasketWithNoShelfAssigned = new SimpleThingState(30);

        //|__Reserved Imported En Route To Shelf
        public static readonly SimpleThingState AvailableInImportBasket = new SimpleThingState(24);
        public static readonly SimpleThingState AvailableInImportBasketWithShelfAssignment = new SimpleThingState(25);
        public static readonly SimpleThingState AvailableInImportBasketWithNoShelfSpaceFound = new SimpleThingState(26);
        public static readonly SimpleThingState AvailableInImportBasketWithNoStaticShelfAssignment = new SimpleThingState(27);
        public static readonly SimpleThingState AvailableInImportBasketWithNoShelfAssigned = new SimpleThingState(31);

        //|__Import Overflow
        public static readonly SimpleThingState AvailableInImportOverflowZone = new SimpleThingState(28);
        public static readonly SimpleThingState HeldInImportOverflowZone = new SimpleThingState(29);

        //public static readonly SimpleThingState InImportBasketWithNoShelfAssigned = new SimpleThingState(1L << 60);

        public static readonly ThingState ReservedForExportInExportWarehouse =
            ReservedInShelfForExport
            | ReservedInCartonForExport
            | ReservedInUnboxingZoneForExport
            | ReservedInExportWarehouseDispatchZone;

        public static readonly ThingState AssignedToShipmentInExportWarehouse =
            HeldInShelfForExport
            | HeldInCartonForExport
            | HeldInUnboxingZoneForExport
            | HeldInExportWarehouseDispatchZone;

        public static readonly ThingState AssignedToShipmentInImportBasket =
            HeldInImportBasket
            | HeldInImportBasketWithShelfAssignment
            | HeldInImportBasketWithNoShelfSpaceFound
            | HeldInImportBasketWithNoStaticShelfAssignment
            | HeldInImportBasketWithNoShelfAssigned;

        public static readonly ThingState UnassignedInImportBasket =
            AvailableInImportBasket
            | AvailableInImportBasketWithShelfAssignment
            | AvailableInImportBasketWithNoShelfSpaceFound
            | AvailableInImportBasketWithNoStaticShelfAssignment
            | AvailableInImportBasketWithNoShelfAssigned;

        public static readonly ThingState AvailableForPurchaseInExportWarehouse =
            AvailableInShelf
            | ExcessAvailableInShelf
            | AvailableInCartonForExport
            | AvailableInUnboxingZoneForExport;

        // InWarehouse sub-states
        public static readonly ThingState InReturnZonePreviouslyAssigned =
            InReturnZoneCancelledWhilePicking
            | InReturnZoneCancelledBeforeDispatch
            | InReturnZoneReturnedFromDelivery
            | InReturnZoneCancelledAfterImport;

        public static readonly ThingState HeldInShelf =
            HeldInShelfForExport
            | HeldInShelfForLocal;

        public static readonly ThingState InImportOverflowZone =
            HeldInImportOverflowZone
            | AvailableInImportOverflowZone;

        public static readonly ThingState InTransport =
            InTransportAssigned
            | InTransportPreviouslyAssigned;

        public static readonly ThingState InPurchaseZone =
            InPurchaseZoneForLocal
            | InPurchaseZoneUnassigned;

        public static readonly ThingState FlaggedForReview =
            FlaggedDamagedInShelf
            | FlaggedExpiredInShelf
            | FlaggedMissingInShelf;

        public static readonly ThingState UnassignedInWarehouse =
            InIncomingZone
            | InIncomingHoldZone
            | InPreSortingZone
            | InIntakeProcessingZone
            | InTransferZone
            | InTransmutationZone
            | InJunkyard
            | InNarnia
            | InGraveyard
            | TransmutedOut
            | InReturnZonePreviouslyAssigned
            | InTransportPreviouslyAssigned
            | InReturnReshelvingZone
            | InPurchaseZoneUnassigned
            | InRecycleZone
            | AvailableInShelf
            | ExcessAvailableInShelf
            | FlaggedForReview
            | InRecycleTransit
            | InRecallTransit
            | UninspectedInVendorReturnZone
            | InReturnZoneNeverAssigned
            | PendingReturnInVendorReturnZone
            | InTransferZoneWithShelfAssignment
            | InReturnReshelvingZoneWithShelfAssignment
            | InTransferZoneWithNoShelfSpaceFound
            | InTransferZoneWithNoStaticShelfAssignment
            | InPerishableTransit
            | AvailableInCartonForExport
            | AvailableInUnboxingZoneForExport
            | ReservedForExportInExportWarehouse
            | UnassignedInImportBasket
            | AvailableInImportOverflowZone
            | InCartonTransferZone
            | InCartonTransferZoneWithShelfAssignment
            | InCartonTransferZoneWithNoShelfSpaceFound
            | InCartonTransferZoneWithNoShelfAssigned
            | InCartonTransferZoneWithNoStaticShelfAssignment;

        public static readonly ThingState AssignedToShipmentInWarehouse =
            HeldInShelf
            | InBasket
            | InReadyBasket
            | InPurchaseZoneForLocal
            | InTransportAssigned
            | AssignedToShipmentInExportWarehouse
            | AssignedToShipmentInImportBasket
            | HeldInImportOverflowZone;

        //public static readonly ThingState MaybeAssignedToShipmentOrUnassignedInWarehouse = InImportBasket | InImportBasketWithShelfAssignment | InImportBasketWithNoShelfAssigned | InImportBasketWithNoShelfSpaceFound | InImportBasketWithNoStaticShelfAssignment;

        public static readonly ThingState EnRouteToShelfFromTransferZone =
            InTransferZone
            | InTransferZoneWithNoShelfAssigned
            | InTransferZoneWithNoShelfSpaceFound
            | InTransferZoneWithNoStaticShelfAssignment
            | InTransferZoneWithShelfAssignment;

        public static readonly ThingState InImportBasket =
            HeldInImportBasket
            | HeldInImportBasketWithShelfAssignment
            | HeldInImportBasketWithNoShelfSpaceFound
            | HeldInImportBasketWithNoStaticShelfAssignment
            | HeldInImportBasketWithNoShelfAssigned
            | AvailableInImportBasket
            | AvailableInImportBasketWithShelfAssignment
            | AvailableInImportBasketWithNoShelfSpaceFound
            | AvailableInImportBasketWithNoStaticShelfAssignment
            | AvailableInImportBasketWithNoShelfAssigned;

        public static readonly ThingState InImportBasketShelvingFailed =
            HeldInImportBasketWithNoShelfSpaceFound
            | AvailableInImportBasketWithNoShelfSpaceFound
            | HeldInImportBasketWithNoStaticShelfAssignment
            | AvailableInImportBasketWithNoStaticShelfAssignment;

        public static readonly ThingState InImportBasketReadyForShelving =
            HeldInImportBasketWithShelfAssignment
            | AvailableInImportBasketWithShelfAssignment
            | HeldInImportBasketWithNoShelfAssigned
            | AvailableInImportBasketWithNoShelfAssigned;

        public static readonly ThingState EnRouteToShelfFromImportBasket =
            HeldInImportBasket
            | AvailableInImportBasket
            | InImportBasketReadyForShelving
            | InImportBasketShelvingFailed;

        public static readonly ThingState EnRouteToShelfFromCartonTransferZone =
            InCartonTransferZone
            | InCartonTransferZoneWithShelfAssignment
            | InCartonTransferZoneWithNoShelfSpaceFound
            | InCartonTransferZoneWithNoShelfAssigned
            | InCartonTransferZoneWithNoStaticShelfAssignment;

        public static readonly ThingState InCartonShelf =
            AvailableInCartonForExport
            | HeldInCartonForExport
            | ReservedInCartonForExport;

        public static readonly ThingState InCarton =
            EnRouteToShelfFromCartonTransferZone
            | InCartonShelf;

        public static readonly ThingState InExportWarehouse =
            EnRouteToShelfFromCartonTransferZone
            | AvailableForPurchaseInExportWarehouse
            | ReservedForExportInExportWarehouse
            | AssignedToShipmentInExportWarehouse;

        // Previously assigned
        public static readonly ThingState PreviouslyAssignedToShipment =
            InTransportPreviouslyAssigned
            | InReturnZonePreviouslyAssigned
            | ReportedMissingWithCustomer
            | InReturnTransit;

        // InTransitToWarehouse
        public static readonly ThingState EnRouteToShelf =
            EnRouteToShelfFromTransferZone
            | InReturnReshelvingZone
            | InReturnReshelvingZoneWithNoShelfAssigned
            | InReturnReshelvingZoneWithNoShelfSpaceFound
            | InReturnReshelvingZoneWithNoStaticShelfAssignment
            | InReturnReshelvingZoneWithShelfAssignment
            | EnRouteToShelfFromImportBasket
            | InPerishableTransit
            | EnRouteToShelfFromCartonTransferZone;

        // Imaginary sub-states
        public static readonly ThingState PendingMarketPurchase = PendingMarketPurchaseForLocal | PendingMarketPurchaseForDeficit;


        // Primary States
        public static readonly ThingState InWarehouse = UnassignedInWarehouse | AssignedToShipmentInWarehouse;
        public static readonly ThingState Imaginary = PendingMarketPurchase | InPrimordialSoup | FailedMarketPurchase | CancelledMarketPurchase;

        // Other composite states
        public static readonly ThingState AssignedToShipment =
            AssignedToShipmentInWarehouse
            | AssignedToShipmentInExportWarehouse
            | PendingMarketPurchaseForLocal
            | WithCustomer;

        public static readonly ThingState InShelf =
            AvailableInShelf
            | ExcessAvailableInShelf
            | HeldInShelf
            | FlaggedDamagedInShelf
            | FlaggedExpiredInShelf
            | FlaggedMissingInShelf
            | ReservedInShelfForExport;

        public static readonly ThingState InCartonUnshelvedButSpaceAssigned =
            InCartonTransferZoneWithShelfAssignment;

        public static readonly ThingState UnshelvedButSpaceAssigned =
            InTransferZoneWithShelfAssignment
            | InReturnReshelvingZoneWithShelfAssignment
            | HeldInImportBasketWithShelfAssignment
            | AvailableInImportBasketWithShelfAssignment;

        public static readonly ThingState HasShipmentContext =
            AssignedToShipment
            | PreviouslyAssignedToShipment;

        public static readonly ThingState HasWarehouseContext =
            InWarehouse
            | EnRouteToShelf
            | CancelledMarketPurchase
            | FailedMarketPurchase
            | PendingMarketPurchase
            | InExportWarehouse;

        public static readonly ThingState HasShelfContext =
            InShelf
            | UnshelvedButSpaceAssigned
            | InCartonShelf
            | InCartonUnshelvedButSpaceAssigned;

        public static readonly ThingState HasDestinationWarehouseContext = ReservedForExportInExportWarehouse;

        public static readonly ThingState UnfulfilledForShipment =
            HeldInShelf
            | HeldInImportBasket
            | InPurchaseZone
            | PendingMarketPurchaseForLocal;

        public static readonly ThingState ActiveThingState =
            InIncomingZone
            | InIncomingHoldZone
            | InPreSortingZone
            | InIntakeProcessingZone
            | InTransferZone
            | InRecycleTransit
            | InRecallTransit
            | InRecycleZone
            | AvailableInShelf
            | ExcessAvailableInShelf
            | FlaggedMissingInShelf
            | FlaggedDamagedInShelf
            | FlaggedExpiredInShelf
            | HeldInShelfForLocal
            | HeldInShelfForExport
            | InReturnReshelvingZone
            | InBasket
            | InReadyBasket
            | InPurchaseZoneForLocal
            | InTransportAssigned
            | PendingMarketPurchaseForLocal
            | UninspectedInVendorReturnZone
            | InReturnZoneNeverAssigned
            | InReturnZonePreviouslyAssigned
            | InPurchaseZoneUnassigned
            | PendingMarketPurchaseForDeficit
            | PendingReturnInVendorReturnZone
            | InTransportPreviouslyAssigned
            | InReturnTransit
            | InPerishableTransit
            | InTransferZoneWithShelfAssignment
            | InReturnReshelvingZoneWithShelfAssignment
            | InTransferZoneWithNoShelfAssigned
            | InReturnReshelvingZoneWithNoShelfAssigned
            | InTransferZoneWithNoShelfSpaceFound
            | InTransferZoneWithNoStaticShelfAssignment
            | InReturnReshelvingZoneWithNoShelfSpaceFound
            | InReturnReshelvingZoneWithNoStaticShelfAssignment
            | HeldInImportOverflowZone
            | AvailableInImportOverflowZone
            | ReservedInShelfForExport
            | InCartonTransferZone
            | InCartonTransferZoneWithShelfAssignment
            | InCartonTransferZoneWithNoShelfSpaceFound
            | InCartonTransferZoneWithNoShelfAssigned
            | InCartonTransferZoneWithNoStaticShelfAssignment
            | AvailableInCartonForExport
            | HeldInCartonForExport
            | ReservedInCartonForExport
            | AvailableInUnboxingZoneForExport
            | HeldInUnboxingZoneForExport
            | ReservedInUnboxingZoneForExport
            | HeldInExportWarehouseDispatchZone
            | ReservedInExportWarehouseDispatchZone
            | HeldInImportBasket
            | AvailableInImportBasket
            | HeldInImportBasketWithShelfAssignment
            | AvailableInImportBasketWithShelfAssignment
            | HeldInImportBasketWithNoShelfSpaceFound
            | AvailableInImportBasketWithNoShelfSpaceFound
            | HeldInImportBasketWithNoStaticShelfAssignment
            | AvailableInImportBasketWithNoStaticShelfAssignment
            | HeldInImportBasketWithNoShelfAssigned
            | AvailableInImportBasketWithNoShelfAssigned;

        public static readonly ThingState AvailableForPurchase =
            AvailableInShelf
            | ExcessAvailableInShelf
            | AvailableInImportOverflowZone;

        public static readonly ThingState InTransferZoneOrReturnReshelvingZoneOrImportBasket =
            InTransferZone
            | InReturnReshelvingZone
            | HeldInImportBasket
            | AvailableInImportBasket;

        public static readonly ThingState InTransferZoneOrReturnReshelvingZoneOrImportBasketWithNoShelfFound =
            InTransferZoneWithNoStaticShelfAssignment
            | InTransferZoneWithNoShelfSpaceFound
            | InReturnReshelvingZoneWithNoShelfSpaceFound
            | InReturnReshelvingZoneWithNoStaticShelfAssignment
            | HeldInImportBasketWithNoShelfSpaceFound
            | AvailableInImportBasketWithNoShelfSpaceFound
            | HeldInImportBasketWithNoStaticShelfAssignment
            | AvailableInImportBasketWithNoStaticShelfAssignment;

        public static readonly ThingState InCartonTransferZoneReadyForShelving =
            InCartonTransferZoneWithShelfAssignment
            | InCartonTransferZoneWithNoShelfAssigned;

        public static readonly ThingState InCartonTransferZoneWithNoShelfFound =
            InCartonTransferZoneWithNoShelfSpaceFound
            | InCartonTransferZoneWithNoStaticShelfAssignment;

        public static readonly ThingState InTransferZoneReadyForShelving =
            InTransferZoneWithShelfAssignment
            | InTransferZoneWithNoShelfAssigned;

        public static readonly ThingState InReturnZoneReadyForShelving =
            InReturnReshelvingZoneWithShelfAssignment
            | InReturnReshelvingZoneWithNoShelfAssigned;

        public static readonly ThingState InReturnReshelvingZoneWithNoShelf =
            InReturnReshelvingZoneWithNoShelfAssigned
            | InReturnReshelvingZoneWithNoShelfSpaceFound
            | InReturnReshelvingZoneWithNoStaticShelfAssignment;

        public static readonly ThingState SourcingState =
            InIncomingHoldZone
            | InPreSortingZone
            | PendingReturnInVendorReturnZone
            | UninspectedInVendorReturnZone
            | InTransferZoneWithShelfAssignment
            | InTransferZoneWithNoShelfAssigned
            | InTransferZoneWithNoShelfSpaceFound
            | InTransferZoneWithNoStaticShelfAssignment
            | InCartonTransferZoneWithShelfAssignment
            | InCartonTransferZoneWithNoShelfAssigned
            | InCartonTransferZoneWithNoShelfSpaceFound
            | InCartonTransferZoneWithNoStaticShelfAssignment
            | InImportBasket
            | InReturnReshelvingZoneWithShelfAssignment
            | InReturnReshelvingZoneWithNoShelfAssigned
            | InReturnReshelvingZoneWithNoShelfSpaceFound
            | InReturnReshelvingZoneWithNoStaticShelfAssignment
            | InRecycleTransit
            | InRecallTransit
            | FlaggedMissingInShelf
            | FlaggedDamagedInShelf
            | FlaggedExpiredInShelf;

        public bool IsSuperSetOf(ThingState other)
        {
            var thisSimpleStateValues = ToSimpleThingStateValues().ToDictionary(i => i, i => true);
            foreach (var simpleStateValue in other.ToSimpleThingStateValues())
            {
                if (!thisSimpleStateValues.ContainsKey(simpleStateValue))
                    return false;
            }

            return true;
        }

        public bool HasAnyOverlap(ThingState other)
        {
            var thisSimpleStateValues = ToSimpleThingStateValues().ToDictionary(i => i, i => true);
            foreach (var simpleStateValue in other.ToSimpleThingStateValues())
            {
                if (thisSimpleStateValues.ContainsKey(simpleStateValue))
                    return true;
            }

            return false;
        }

        // State Metadata
        // We'll use Reflection to get the names of states
        // This will be used for various forms and graphs on the admin panel, and for serialization on the Warehouse Ops app
        public static readonly IReadOnlyDictionary<string, SimpleThingState> SimpleThingStatesByName;
        public static readonly IReadOnlyDictionary<long, string> SimpleThingStateNamesByStateValue;
        public static readonly IReadOnlyDictionary<string, CompositeThingState> CompositeThingStatesByName;
        public static readonly IReadOnlyDictionary<CompositeThingState, string> CompositeThingStateNamesByState;

        protected static readonly bool IsMetaDataInitialized;

        static ThingState()
        {
            // This will help map various simple and composite states to string names based on the variable name
            var allFields =
                typeof(ThingState).GetFields(BindingFlags.Static | BindingFlags.Public)
                    .Where(fi => fi.IsInitOnly) // Only consider readonly fields in this class
                    .Where(fi => fi.FieldType.In(typeof(ThingState), typeof(SimpleThingState), typeof(CompositeThingState)))
                    .Select(fi => new { FieldInfo = fi, Value = fi.GetValue(null) })
                    .ToList();

            SimpleThingStatesByName =
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, SimpleThingState>(
                        allFields
                            .Where(i => i.FieldInfo.FieldType == typeof(SimpleThingState))
                            .ToDictionary(i => i.FieldInfo.Name, i => (SimpleThingState)i.Value)
                    );

            SimpleThingStateNamesByStateValue =
                    new System.Collections.ObjectModel.ReadOnlyDictionary<long, string>(
                        SimpleThingStatesByName.ToDictionary(kv => kv.Value.Value, kv => kv.Key)
                    );

            CompositeThingStatesByName =
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, CompositeThingState>(
                        allFields
                            .Where(i => i.FieldInfo.FieldType != typeof(SimpleThingState) && i.Value.GetType() == typeof(CompositeThingState))
                            .ToDictionary(i => i.FieldInfo.Name, i => (CompositeThingState)i.Value)
                    );

            CompositeThingStateNamesByState =
                    new System.Collections.ObjectModel.ReadOnlyDictionary<CompositeThingState, string>(
                        CompositeThingStatesByName
                            .GroupBy(kv => kv.Value)
                            .ToDictionary(g => g.Key, g => g.First().Key)
                    );

            IsMetaDataInitialized = true;
        }

        public static bool TryParse(string stateStr, out ThingState state)
        {
            if (SimpleThingState.TryParse(stateStr, out SimpleThingState simpleState))
            {
                state = simpleState;
                return true;
            }
            else if (CompositeThingState.TryParse(stateStr, out CompositeThingState compositeState))
            {
                state = compositeState;
                return true;
            }
            else
            {
                state = null;
                return false;
            }
        }
    }

#if !DB
    [JsonConverter(typeof(ThingStateJsonConverter))]
#endif
    public class SimpleThingState : ThingState, IEquatable<SimpleThingState>
    {
        public SimpleThingState(long value)
        {
            // These values declared statically in ThingState are constructed before the metadata maps are built
            // So only apply validation for later constructions
            if (IsMetaDataInitialized && !SimpleThingStateNamesByStateValue.ContainsKey(value))
            {
                throw new ArgumentException($"Invalid SimpleThingState value: {value}");
            }

            Value = value;

            // These are hotspots that are created multiple times in tight loops
            // So we can cache them
            _thisAsArray = new[] { this };
            _thisAsArrayValue = new[] { value };
        }

        public long Value { get; }

        private readonly SimpleThingState[] _thisAsArray;
        private readonly long[] _thisAsArrayValue;

        public override bool Equals(object obj)
        {
            if (!(obj is SimpleThingState objAs))
            {
                return false;
            }

            return Value == objAs.Value;
        }

        public bool Equals(SimpleThingState other) => this.Value == other.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override SimpleThingState[] ToSimpleThingStates() => _thisAsArray;
        public override long[] ToSimpleThingStateValues() => _thisAsArrayValue;

        public static explicit operator SimpleThingState(long value) => new SimpleThingState(value);
        public static explicit operator long(SimpleThingState state) => state.Value;

        public static bool operator ==(SimpleThingState thisState, SimpleThingState otherState)
            => thisState?.Value == otherState?.Value;

        public static bool operator !=(SimpleThingState thisState, SimpleThingState otherState)
            => thisState?.Value != otherState?.Value;

        public static bool operator ==(SimpleThingState thisState, ThingState otherState)
            => otherState is SimpleThingState otherSimpleState && thisState?.Value == otherSimpleState?.Value;

        public static bool operator !=(SimpleThingState thisState, ThingState otherState)
            => !(otherState is SimpleThingState otherSimpleState && thisState?.Value == otherSimpleState?.Value);

        public override string ToString()
        {
            if (SimpleThingStateNamesByStateValue.TryGetValue(Value, out string name))
                return name;
            else
                // Shouldn't happen
                return $"Unknown State: {Value}";
        }

        public static bool TryParse(string stateStr, out SimpleThingState state) => SimpleThingStatesByName.TryGetValue(stateStr, out state);

        public static bool IsValidSimpleThingStateValue(long value) => SimpleThingStateNamesByStateValue.ContainsKey(value);
    }

#if !DB
    [JsonConverter(typeof(ThingStateJsonConverter))]
#endif
    public class CompositeThingState : ThingState, IEquatable<CompositeThingState>
    {
        private readonly SimpleThingState[] _simpleStates;
        private readonly long[] _values;

        public CompositeThingState(ThingState a, ThingState b)
            : this(a.ToSimpleThingStates().Concat(b.ToSimpleThingStates()))
        {

        }

        private CompositeThingState(IEnumerable<SimpleThingState> simpleStates)
        {
            _simpleStates =
                simpleStates
                    // Sort and distinct, needed for equality
                    .Distinct().OrderBy(i => i.Value)
                    .ToArray();

            _values =
                _simpleStates
                    .Select(i => i.Value)
                    .ToArray();
        }

        public bool Equals(CompositeThingState other) => _simpleStates.SequenceEqual(other._simpleStates);

        public override bool Equals(object obj)
        {
            if (!(obj is CompositeThingState objAs))
            {
                return false;
            }

            return _simpleStates.SequenceEqual(objAs._simpleStates);
        }

        public static bool operator ==(CompositeThingState thisState, CompositeThingState otherState)
            => (thisState?._simpleStates ?? new SimpleThingState[] { }).SequenceEqual(otherState?._simpleStates ?? new SimpleThingState[] { });

        public static bool operator !=(CompositeThingState thisState, CompositeThingState otherState)
            => !((thisState?._simpleStates ?? new SimpleThingState[] { }).SequenceEqual(otherState?._simpleStates ?? new SimpleThingState[] { }));

        public static bool operator ==(CompositeThingState thisState, ThingState otherState)
            => otherState is CompositeThingState otherCompositeState &&
                (thisState?._simpleStates ?? new SimpleThingState[] { }).SequenceEqual(otherCompositeState?._simpleStates ?? new SimpleThingState[] { });

        public static bool operator !=(CompositeThingState thisState, ThingState otherState)
            => !(otherState is CompositeThingState otherCompositeState &&
                (thisState?._simpleStates ?? new SimpleThingState[] { }).SequenceEqual(otherCompositeState?._simpleStates ?? new SimpleThingState[] { }));

        // A hash code based on structural comparsion of the array
        // NOTE: _values.GetHashCode is incorrect here!
        public override int GetHashCode() =>
            ((IStructuralEquatable)_values).GetHashCode(EqualityComparer<long>.Default);

        public override SimpleThingState[] ToSimpleThingStates() => _simpleStates;
        public override long[] ToSimpleThingStateValues() => _values;

        public override string ToString()
        {
            if (CompositeThingStateNamesByState.TryGetValue(this, out string name))
                return name;
            else
            {
                // Not all composite states have names
                return string.Join("|", _simpleStates.Select(s => s.ToString()));
            }
        }

        public static bool TryParse(string stateStr, out CompositeThingState state)
        {
            if (CompositeThingStatesByName.TryGetValue(stateStr, out state))
                return true;
            else
            {
                var stateStrParts = stateStr.Split('|');
                var parsedStates = new List<SimpleThingState>(stateStrParts.Length);
                foreach (var part in stateStrParts)
                {
                    if (SimpleThingState.TryParse(part, out var simpleState))
                        parsedStates.Add(simpleState);
                    else
                        return false;
                }

                if (parsedStates.Count > 1) // if equal to 1, then it must be a simple state
                {
                    state = new CompositeThingState(parsedStates);
                    return true;
                }
                else
                    return false;
            }
        }
    }

#if !DB
    class ThingStateJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            (objectType == typeof(ThingState) || objectType == typeof(CompositeThingState) || objectType == typeof(SimpleThingState));

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var stateStr = serializer.Deserialize<string>(reader);
            if (ThingState.TryParse(stateStr, out ThingState state))
                return state;
            else
                throw new InvalidOperationException($"Unable to deserialize {stateStr} into ThingState");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToString());
        }
    }
#endif

    // Some language features such as overloading == and != aren't
    // directly testable using F# testing library
    public static class ThingState_Test_CSharp_LanguageFeatures
    {
        public static void Test()
        {
            // Simple States

            // Test equality
            if (!(ThingState.InPrimordialSoup == new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.WithCustomer != new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test equality
            if (!(ThingState.InPrimordialSoup == (ThingState)new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.WithCustomer != (ThingState)new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Composite States

            // Test equality
            if (!((CompositeThingState)ThingState.HeldInShelf == (CompositeThingState)(ThingState.HeldInShelfForExport | ThingState.HeldInShelfForLocal)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((CompositeThingState)ThingState.HeldInShelf != (CompositeThingState)(ThingState.HeldInShelfForExport | ThingState.WithCustomer)))
                throw new InvalidOperationException("Fail");

            // Test equality
            if (!((CompositeThingState)ThingState.HeldInShelf == (ThingState.HeldInShelfForExport | ThingState.HeldInShelfForLocal)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((CompositeThingState)ThingState.HeldInShelf != (ThingState.HeldInShelfForExport | ThingState.WithCustomer)))
                throw new InvalidOperationException("Fail");

            // Thing States
            // Test equality
            if (!((ThingState)ThingState.InPrimordialSoup == new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((ThingState)ThingState.WithCustomer != new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test equality
            if (!((ThingState)ThingState.InPrimordialSoup == (ThingState)new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((ThingState)ThingState.WithCustomer != (ThingState)new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            if (!(ThingState.HeldInShelf == ((CompositeThingState)(ThingState.HeldInShelfForExport | ThingState.HeldInShelfForLocal))))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.HeldInShelf != ((CompositeThingState)(ThingState.HeldInShelfForExport | ThingState.WithCustomer))))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((ThingState)ThingState.WithCustomer != ThingState.HeldInShelf))
                throw new InvalidOperationException("Fail");

            // Test equality
            if (!(ThingState.InPrimordialSoup == new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.WithCustomer != new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test equality
            if (!(ThingState.InPrimordialSoup == (ThingState)new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.WithCustomer != (ThingState)new SimpleThingState(ThingState.InPrimordialSoup.Value)))
                throw new InvalidOperationException("Fail");

            // Simple States

            // Test equality
            if (ThingState.InPrimordialSoup == null)
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.WithCustomer != null))
                throw new InvalidOperationException("Fail");

            // Test equality
            if (ThingState.InPrimordialSoup == null)
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.WithCustomer != (ThingState)null))
                throw new InvalidOperationException("Fail");

            // Composite States

            // Test equality
            if ((CompositeThingState)ThingState.HeldInShelf == null)
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((CompositeThingState)ThingState.HeldInShelf != null))
                throw new InvalidOperationException("Fail");

            // Test equality
            if ((CompositeThingState)ThingState.HeldInShelf == null)
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((CompositeThingState)ThingState.HeldInShelf != null))
                throw new InvalidOperationException("Fail");

            // Thing States
            // Test equality
            if ((ThingState)ThingState.InPrimordialSoup == (SimpleThingState)null)
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((ThingState)ThingState.WithCustomer != (SimpleThingState)null))
                throw new InvalidOperationException("Fail");

            // Test equality
            if ((ThingState)ThingState.InPrimordialSoup == (ThingState)null)
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((ThingState)ThingState.WithCustomer != (ThingState)null))
                throw new InvalidOperationException("Fail");

            if (ThingState.HeldInShelf == ((CompositeThingState)null))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!(ThingState.HeldInShelf != ((CompositeThingState)null)))
                throw new InvalidOperationException("Fail");

            // Test not equality
            if (!((ThingState)ThingState.WithCustomer != (ThingState)null))
                throw new InvalidOperationException("Fail");
        }
    }
}
