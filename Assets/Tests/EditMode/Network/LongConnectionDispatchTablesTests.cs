using System;
using System.Collections.Generic;
using Network;
using NUnit.Framework;

namespace Tests.EditMode.Network {
    [TestFixture]
    public sealed class LongConnectionDispatchTablesTests {
        [Test]
        public void Ctor_CopiesInputDictionaries() {
            var main = new Dictionary<int, LongConnectionMainDispatchEntry> {
                { 1, new LongConnectionMainDispatchEntry(typeof(string), _ => { }) },
            };
            var push = new Dictionary<int, Action> {
                { 9, () => { } },
            };

            var tables = new LongConnectionDispatchTables(main, push);

            main[2] = new LongConnectionMainDispatchEntry(typeof(int), _ => { });
            push[10] = () => { };

            Assert.That(tables.MainByTypeInt.Count, Is.EqualTo(1));
            Assert.That(tables.PushByMarker.Count, Is.EqualTo(1));
            Assert.That(tables.DispatchPushWhenMainTypeUnknown, Is.True);
        }

        [Test]
        public void Ctor_NullMainDictionary_Throws() {
            var push = new Dictionary<int, Action>();

            Assert.Throws<ArgumentNullException>(() =>
                    _ = new LongConnectionDispatchTables(null, push));
        }

        [Test]
        public void Ctor_NullPushDictionary_Throws() {
            var main = new Dictionary<int, LongConnectionMainDispatchEntry>();

            Assert.Throws<ArgumentNullException>(() =>
                    _ = new LongConnectionDispatchTables(main, null));
        }
    }
}
