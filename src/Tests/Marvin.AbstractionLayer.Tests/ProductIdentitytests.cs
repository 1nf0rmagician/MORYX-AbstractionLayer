﻿using System;
using NUnit.Framework;

namespace Marvin.AbstractionLayer.Tests
{
    [TestFixture]
    public class ProductIdentityTests
    {
        private const string Identifier = "4564654";
        private const short Revision = 5;

        private ProductIdentity _identity;

        [SetUp]
        public void Setup()
        {
            _identity = new ProductIdentity(Identifier, Revision);
        }

        /// <summary>
        /// Tests the ToString Method, will be used in serveral components which will using only the identifier string
        /// Example: Communication with SAP, don't change the result of the method!
        /// </summary>
        [Test]
        public void ToStringTest()
        {
            var expectedString = string.Format("{0}-{1:D2}", Identifier, Revision);

            Assert.AreEqual(expectedString, _identity.ToString());
        }

        /// <summary>
        /// Set identifier should throw an exception because changing the identifier is not allowed
        /// </summary>
        [Test]
        public void SetIdentifierTest()
        {
            Assert.Throws(typeof(InvalidOperationException), () => _identity.SetIdentifier("HelloWorld"));
        }
    }
}