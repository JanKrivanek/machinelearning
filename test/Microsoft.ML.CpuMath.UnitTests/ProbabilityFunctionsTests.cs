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
        public ProbabilityFunctionsTests(ITestOutputHelper output) : base(output) { }

        // -----------------------------------------------------------------------
        // Erfinv – boundary / special values
        // -----------------------------------------------------------------------

        [Fact]
        public void Erfinv_OutOfRangeAbove_ReturnsNaN()
        {
            double result = ProbabilityFunctions.Erfinv(1.5);
            Assert.True(double.IsNaN(result), $"Expected NaN for Erfinv(1.5) but got {result}");
        }

        [Fact]
        public void Erfinv_OutOfRangeBelow_ReturnsNaN()
        {
            double result = ProbabilityFunctions.Erfinv(-1.5);
            Assert.True(double.IsNaN(result), $"Expected NaN for Erfinv(-1.5) but got {result}");
        }

        [Fact]
        public void Erfinv_PositiveOne_ReturnsPositiveInfinity()
        {
            double result = ProbabilityFunctions.Erfinv(1.0);
            Assert.True(double.IsPositiveInfinity(result), $"Expected +Infinity for Erfinv(1) but got {result}");
        }

        [Fact]
        public void Erfinv_NegativeOne_ReturnsNegativeInfinity()
        {
            double result = ProbabilityFunctions.Erfinv(-1.0);
            Assert.True(double.IsNegativeInfinity(result), $"Expected -Infinity for Erfinv(-1) but got {result}");
        }

        [Fact]
        public void Erfinv_Zero_ReturnsZero()
        {
            double result = ProbabilityFunctions.Erfinv(0.0);
            Assert.True(Math.Abs(result) < 1e-10, $"Expected 0 for Erfinv(0) but got {result}");
        }

        // -----------------------------------------------------------------------
        // Erfinv – consistency (coefficient caching must not affect output)
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(0.9)]
        [InlineData(-0.9)]
        public void Erfinv_RepeatedCalls_ReturnIdenticalResults(double x)
        {
            double first = ProbabilityFunctions.Erfinv(x);
            double second = ProbabilityFunctions.Erfinv(x);
            double third = ProbabilityFunctions.Erfinv(x);
            Assert.Equal(first, second);
            Assert.Equal(first, third);
        }

        // -----------------------------------------------------------------------
        // Erfinv / Erf round-trip: Erf(Erfinv(x)) ≈ x
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.1)]
        [InlineData(-0.1)]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(0.8)]
        [InlineData(-0.8)]
        [InlineData(0.99)]
        [InlineData(-0.99)]
        public void Erf_Erfinv_RoundTrip(double x)
        {
            double roundTripped = ProbabilityFunctions.Erf(ProbabilityFunctions.Erfinv(x));
            Assert.True(Math.Abs(roundTripped - x) < 1e-6,
                $"Erf(Erfinv({x})) = {roundTripped}, expected ~{x}");
        }

        // -----------------------------------------------------------------------
        // Erf – special values
        // -----------------------------------------------------------------------

        [Fact]
        public void Erf_PositiveInfinity_ReturnsOne()
        {
            double result = ProbabilityFunctions.Erf(double.PositiveInfinity);
            Assert.True(Math.Abs(result - 1.0) < 1e-15, $"Expected 1.0 for Erf(+inf) but got {result}");
        }

        [Fact]
        public void Erf_NegativeInfinity_ReturnsNegativeOne()
        {
            double result = ProbabilityFunctions.Erf(double.NegativeInfinity);
            Assert.True(Math.Abs(result - (-1.0)) < 1e-15, $"Expected -1.0 for Erf(-inf) but got {result}");
        }

        [Fact]
        public void Erf_Zero_ReturnsZero()
        {
            // The polynomial approximation has ~1e-9 error at x=0; use a generous tolerance.
            double result = ProbabilityFunctions.Erf(0.0);
            Assert.True(Math.Abs(result) < 1e-6, $"Expected ~0 for Erf(0) but got {result}");
        }

        // -----------------------------------------------------------------------
        // Erfc – special values
        // -----------------------------------------------------------------------

        [Fact]
        public void Erfc_PositiveInfinity_ReturnsZero()
        {
            double result = ProbabilityFunctions.Erfc(double.PositiveInfinity);
            Assert.True(Math.Abs(result) < 1e-15, $"Expected 0 for Erfc(+inf) but got {result}");
        }

        [Fact]
        public void Erfc_NegativeInfinity_ReturnsTwo()
        {
            double result = ProbabilityFunctions.Erfc(double.NegativeInfinity);
            Assert.True(Math.Abs(result - 2.0) < 1e-15, $"Expected 2 for Erfc(-inf) but got {result}");
        }

        [Fact]
        public void Erfc_Zero_ReturnsOne()
        {
            double result = ProbabilityFunctions.Erfc(0.0);
            Assert.True(Math.Abs(result - 1.0) < 1e-6, $"Expected 1 for Erfc(0) but got {result}");
        }

        // -----------------------------------------------------------------------
        // Erf + Erfc = 1 (complement identity)
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(2.0)]
        [InlineData(-2.0)]
        [InlineData(0.001)]
        public void Erf_Plus_Erfc_EqualsOne(double x)
        {
            double sum = ProbabilityFunctions.Erf(x) + ProbabilityFunctions.Erfc(x);
            Assert.True(Math.Abs(sum - 1.0) < 1e-6,
                $"Erf({x}) + Erfc({x}) = {sum}, expected ~1.0");
        }

        // -----------------------------------------------------------------------
        // Erf symmetry: Erf(-x) == -Erf(x)
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0.5)]
        [InlineData(1.0)]
        [InlineData(1.5)]
        [InlineData(0.123)]
        [InlineData(3.0)]
        public void Erf_IsOddFunction(double x)
        {
            double erfPos = ProbabilityFunctions.Erf(x);
            double erfNeg = ProbabilityFunctions.Erf(-x);
            Assert.True(Math.Abs(erfPos + erfNeg) < 1e-10,
                $"Erf({x}) + Erf(-{x}) = {erfPos + erfNeg}, expected 0 (odd symmetry)");
        }

        // -----------------------------------------------------------------------
        // Erfc symmetry: Erfc(-x) == 2 - Erfc(x)
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0.5)]
        [InlineData(1.0)]
        [InlineData(1.5)]
        [InlineData(0.123)]
        [InlineData(3.0)]
        public void Erfc_SymmetryIdentity(double x)
        {
            double erfcPos = ProbabilityFunctions.Erfc(x);
            double erfcNeg = ProbabilityFunctions.Erfc(-x);
            Assert.True(Math.Abs(erfcNeg - (2.0 - erfcPos)) < 1e-10,
                $"Erfc(-{x}) = {erfcNeg}, expected 2 - Erfc({x}) = {2.0 - erfcPos}");
        }

        // -----------------------------------------------------------------------
        // Probit – valid inputs return finite doubles
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0.5)]
        [InlineData(0.1)]
        [InlineData(0.9)]
        [InlineData(0.01)]
        [InlineData(0.99)]
        public void Probit_ValidInput_ReturnsFiniteDouble(double p)
        {
            double result = ProbabilityFunctions.Probit(p);
            Assert.True(double.IsFinite(result), $"Probit({p}) = {result}, expected a finite double");
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        public void Probit_BoundaryInput_DoesNotThrow(double p)
        {
            // Probit(0) = -∞ and Probit(1) = +∞ mathematically; the implementation
            // accepts these values (no exception) even if the result is not finite.
            var ex = Record.Exception(() => ProbabilityFunctions.Probit(p));
            Assert.Null(ex);
        }

        [Fact]
        public void Probit_HalfReturnsZero()
        {
            // Probit(0.5) should be 0 because the median of the standard normal is 0
            double result = ProbabilityFunctions.Probit(0.5);
            Assert.True(Math.Abs(result) < 1e-6, $"Expected ~0 for Probit(0.5) but got {result}");
        }

        // -----------------------------------------------------------------------
        // Probit – invalid inputs throw
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(-0.001)]
        [InlineData(-1.0)]
        [InlineData(1.001)]
        [InlineData(2.0)]
        public void Probit_InvalidInput_Throws(double p)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityFunctions.Probit(p));
        }

        // -----------------------------------------------------------------------
        // Erfinv – known values (spot checks)
        // -----------------------------------------------------------------------

        [Fact]
        public void Erfinv_KnownValue_HalfSqrtPiOverTwo()
        {
            // Erf(x) = 0.5 => x ≈ 0.4769362762044699
            double x = ProbabilityFunctions.Erfinv(0.5);
            Assert.True(Math.Abs(x - 0.4769362762044699) < 1e-4,
                $"Erfinv(0.5) = {x}, expected ~0.4769");
        }

        [Fact]
        public void Erfinv_Symmetry()
        {
            // Erfinv should be an odd function: Erfinv(-x) == -Erfinv(x)
            double pos = ProbabilityFunctions.Erfinv(0.7);
            double neg = ProbabilityFunctions.Erfinv(-0.7);
            Assert.True(Math.Abs(pos + neg) < 1e-6,
                $"Erfinv(0.7) + Erfinv(-0.7) = {pos + neg}, expected 0");
        }
    }
}
