# MathLib Project

A comprehensive mathematics library demonstrating DevPilot's capabilities.

## Architecture

- **MathLib.dll**: Core library with multiple calculator implementations
- **MathLib.Tests.dll**: xUnit test suite with comprehensive coverage

## Project Structure

- `MathLib/` - Main library project
- `MathLib.Tests/` - Test project

## Coding Standards

**Namespace**: All classes should use `namespace MathLib;` (file-scoped)

**XML Documentation**: Required for all public APIs
- Include `<summary>`, `<param>`, `<returns>`
- Use `<exception>` tags for documented exceptions
- Add `<remarks>` for implementation notes
- Provide `<example>` with code samples

**C# Style**:
- Use expression-bodied members for simple operations
- File-scoped namespaces
- Nullable reference types enabled
- Use `double` for all numeric operations

## Testing Philosophy

**Coverage Requirements**:
- Minimum 3-5 tests per public method
- Test happy path, edge cases, and exceptions
- Test naming: `MethodName_Scenario_ExpectedResult`
- Use Arrange-Act-Assert structure

**Floating-Point Precision**:
- Use `Assert.Equal(expected, actual, precision: 5)` for `double` values
- For transcendental functions (Sin, Cos, Sqrt, Log), use precision: 4

## Classes to Implement

### BasicCalculator
Standard arithmetic operations:
- Add, Subtract, Multiply, Divide
- Modulo (remainder)
- Absolute value

### ScientificCalculator
Advanced mathematical functions:
- Power (x^y)
- Square root
- Logarithm (base 10 and natural)
- Trigonometric functions (Sin, Cos, Tan)

### StatisticsCalculator
Statistical operations:
- Mean (average)
- Median
- Standard deviation
- Min/Max from array

## Quality Expectations

- **Plan Quality**: ≥9.0/10
- **Code Quality**: ≥8.5/10
- **Test Coverage**: ≥9.0/10 (with actual coverage percentage)
- **Documentation**: ≥9.0/10
- **Overall**: ≥8.5/10

## Common Pitfalls to Avoid

❌ **Missing using statements** - Include `using System;` for Math functions
❌ **Overly strict precision** - Don't use precision > 7 for floating-point tests
❌ **Incomplete edge cases** - Test zero, negative, infinity, NaN
❌ **Missing null checks** - Validate array parameters in Statistics methods
