using Abyss.Runtime.Stage;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    public sealed class MaintenanceSessionRegressionTests
    {
        [Test]
        public void ShopReopeningPreservesStockAndOnlyWithdrawsDraftsOnce()
        {
            var session = new MaintenanceSession(7, null, null);
            Assert.That(session.TryBeginShop(), Is.True);
            Assert.That(session.TryStockShop(6), Is.True);
            var counts = session.ShopPurchasedCounts;
            var offers = session.ShopWeaponOffers;
            counts[0] = 1;
            session.AddShopPendingDrafts(2);
            Assert.That(session.TryBeginRest(), Is.False);
            Assert.That(session.TakeShopPendingDrafts(), Is.EqualTo(2));
            Assert.That(session.TakeShopPendingDrafts(), Is.Zero);
            session.ReleaseShop();
            Assert.That(session.TryBeginShop(), Is.True);
            Assert.That(session.TryStockShop(6), Is.False);
            Assert.That(session.ShopPurchasedCounts, Is.SameAs(counts));
            Assert.That(session.ShopWeaponOffers, Is.SameAs(offers));
            Assert.That(session.ShopPurchasedCounts[0], Is.EqualTo(1));
        }

        [Test]
        public void RestMustFinishModalsBeforeSingleExitAndShopRemainsAvailable()
        {
            var session = new MaintenanceSession(7, null, null);
            Assert.That(session.TryBeginRest(), Is.True);
            Assert.That(session.TryBeginShop(), Is.False);
            Assert.That(session.TryMarkExitOpened(), Is.False);
            Assert.That(session.TryConfirmRest(), Is.True);
            Assert.That(session.TryMarkExitOpened(), Is.False);
            Assert.That(session.TryBeginShop(), Is.False);
            Assert.That(session.TryResolveRest(), Is.True);
            Assert.That(session.TryMarkExitOpened(), Is.True);
            Assert.That(session.TryMarkExitOpened(), Is.False);
            Assert.That(session.TryBeginRest(), Is.False);
            Assert.That(session.TryResolveRest(), Is.False);
            Assert.That(session.TryBeginShop(), Is.True);
        }

        [Test]
        public void FailedPanelOpeningAllowsRetryWithoutOpeningExit()
        {
            var session = new MaintenanceSession(7, null, null);
            Assert.That(session.TryBeginRest(), Is.True);
            session.CancelRest();
            Assert.That(session.ActiveService, Is.EqualTo(MaintenanceService.None));
            Assert.That(session.IsExitOpened, Is.False);
            Assert.That(session.TryBeginRest(), Is.True);
        }
    }
}
