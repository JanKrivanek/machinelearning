// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.ML.Internal.CpuMath;
using Microsoft.ML.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ML.CpuMath.UnitTests
{
    public class ProbabilityFunctionsTests : BaseTestClass
    {
        public ProbabilityFunctionsTests(ITestOutputHelper output) : base(output)
        {
        }

        // Erfinv edge cases

        [Fact]
        public void ErfinvOutOfRangeAboveOneReturnsNaN()
        {
            Assert.True(double.IsNaN(ProbabilityFunctions.Erfinv(1.0001)));
        }

        [Fact]
        public void ErfinvOutOfRangeBelowNegativeOneReturnsNaN()
        {
            Assert.True(double.IsNaN(ProbabilityFunctions.Erfinv(-1.0001)));
        }

        [Fact]
        public void ErfinvAtOneReturnsPositiveInfinity()
        {
            Assert.Equal(double.PositiveInfinity, ProbabilityFunctions.Erfinv(1.0));
        }

        [Fact]
        public void ErfinvAtNegativeOneReturnsNegativeInfinity()
        {
            Assert.Equal(double.NegativeInfinity, ProbabilityFunctions.Erfinv(-1.0));
        }

        [Fact]
        public void ErfinvAtZeroReturnsZero()
        {
            Assert.Equal(0.0, ProbabilityFunctions.Erfinv(0.0));
        }

        // Erfinv should be the inverse of Erf: Erfinv(Erf(x)) ≈ x for |x| well within range

        [Theory]
        [InlineData(0.1)]
        [InlineData(0.5)]
        [InlineData(-0.3)]
        [InlineData(0.9)]
        [InlineData(-0.7)]
        public void ErfinvIsInverseOfErf(double x)
        {
            double erfX = ProbabilityFunctions.Erf(x);
            double roundTrip = ProbabilityFunctions.Erfinv(erfX);
            Assert.Equal(x, roundTrip, precision: 5);
        }

        // Erfinv should be an odd function: Erfinv(-x) == -Erfinv(x)

        [Theory]
        [InlineData(0.2)]
        [InlineData(0.6)]
        [InlineData(0.95)]
        public void ErfinvIsOddFunction(double x)
        {
            double positive = ProbabilityFunctions.Erfinv(x);
            double negative = ProbabilityFunctions.Erfinv(-x);
            Assert.Equal(-positive, negative, precision: 10);
        }

        // Calling Erfinv multiple times should yield the same result (coefficients shared, not recomputed)

        [Fact]
        public void ErfinvRepeatedCallsReturnConsistentResults()
        {
            double first = ProbabilityFunctions.Erfinv(0.5);
            double second = ProbabilityFunctions.Erfinv(0.5);
            double third = ProbabilityFunctions.Erfinv(0.5);
            Assert.Equal(first, second);
            Assert.Equal(second, third);
        }

        // Erf edge cases

        [Fact]
        public void ErfAtPositiveInfinityReturnsOne()
        {
            Assert.Equal(1.0, ProbabilityFunctions.Erf(double.PositiveInfinity));
        }

        [Fact]
        public void ErfAtNegativeInfinityReturnsNegativeOne()
        {
            Assert.Equal(-1.0, ProbabilityFunctions.Erf(double.NegativeInfinity));
        }

        [Fact]
        public void ErfAtZeroReturnsZero()
        {
            Assert.Equal(0.0, ProbabilityFunctions.Erf(0.0));
        }

        // Erfc edge cases

        [Fact]
        public void ErfcAtPositiveInfinityReturnsZero()
        {
            Assert.Equal(0.0, ProbabilityFunctions.Erfc(double.PositiveInfinity));
        }

        [Fact]
        public void ErfcAtNegativeInfinityReturnsTwo()
        {
            Assert.Equal(2.0, ProbabilityFunctions.Erfc(double.NegativeInfinity));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(1.0)]
        public void ErfAndErfcSumToOne(double x)
        {
            double erf = ProbabilityFunctions.Erf(x);
            double erfc = ProbabilityFunctions.Erfc(x);
            Assert.Equal(1.0, erf + erfc, precision: 10);
        }
    }
}
