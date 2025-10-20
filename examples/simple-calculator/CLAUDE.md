# Simple Calculator Project

This is a minimal calculator library demonstrating DevPilot usage.

## Architecture

- **Calculator.dll**: Core library with arithmetic operations (`double` precision)
- **Calculator.Tests.dll**: xUnit test suite with comprehensive coverage

## Coding Standards

**XML Documentation**:
- All public APIs MUST have XML documentation
- Include `<summary>`, `<param>`, `<returns>`, and `<remarks>` where applicable
- Use `<exception>` tags for documented exceptions

**C# Style**:
- Use expression-bodied members for simple one-line methods
- File-scoped namespaces (`namespace Calculator;`)
- Nullable reference types enabled
- Use `double` for all numeric operations (NOT `int` or `decimal`)

## Testing Philosophy

**Test Coverage**:
- Minimum 2 tests per public method (happy path + edge case)
- Test names follow pattern: `MethodName_Scenario_ExpectedResult`
- Use Arrange-Act-Assert structure

**Floating-Point Assertions**:
- Always use `Assert.Equal(expected, actual, precision: 5)` for `double` values
- Precision of 5 is appropriate for basic arithmetic
- For transcendental functions (Sin, Cos, Sqrt), use precision: 4

## Common Patterns in This Codebase

### Adding New Arithmetic Operations

When adding a new operation (e.g., Multiply, Divide, Power), follow this template:

**Implementation Pattern**:
```csharp
/// <summary>
/// Brief description of what the operation does.
/// </summary>
/// <param name="a">Description of first operand.</param>
/// <param name="b">Description of second operand.</param>
/// <returns>Description of the result.</returns>
/// <exception cref="DivideByZeroException">
/// Thrown when divisor is zero (if applicable).
/// </exception>
/// <remarks>
/// Additional context, mathematical properties, or usage examples.
/// </remarks>
/// <example>
/// <code>
/// var calc = new Calculator();
/// var result = calc.OperationName(10.0, 2.0);
/// // Returns: expected value
/// </code>
/// </example>
public double OperationName(double a, double b)
{
    // Validation if needed (e.g., division by zero)
    if (b == 0)
        throw new DivideByZeroException("Cannot divide by zero.");

    return /* implementation */;
}
```

**Test Pattern**:
```csharp
public class CalculatorTests
{
    [Fact]
    public void OperationName_TypicalCase_ReturnsExpectedResult()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        var result = calc.OperationName(a, b);

        // Assert
        Assert.Equal(expected, result, precision: 5);
    }

    [Fact]
    public void OperationName_EdgeCase_HandlesCorrectly()
    {
        // Test zero, negative numbers, boundary conditions, etc.
    }

    [Fact]
    public void OperationName_InvalidInput_ThrowsException()
    {
        // Arrange
        var calc = new Calculator();

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => calc.OperationName(x, y));
    }
}
```

## Examples

### How Add and Subtract Are Implemented

Look at `Calculator.cs` for the canonical implementation style:
- Expression-bodied members for simple operations
- Comprehensive XML documentation
- Parameter names that are clear and concise

### How Tests Are Written

Look at `CalculatorTests.cs` for the canonical test style:
- Arrange-Act-Assert structure
- Descriptive test names
- Appropriate floating-point precision
- Edge case coverage (negative numbers, zero, etc.)

## Quality Expectations

When DevPilot generates code for this project:
- **Plan Quality**: ≥9.0/10 (clear, actionable, appropriate scope)
- **Code Quality**: ≥8.5/10 (follows patterns, well-documented, maintainable)
- **Test Coverage**: ≥9.0/10 (comprehensive, covers edge cases)
- **Documentation**: ≥9.0/10 (XML docs complete, examples provided)
- **Overall**: ≥8.5/10

## Common Pitfalls to Avoid

❌ **Don't use `int` for arithmetic** - Use `double` for all operations
❌ **Don't skip XML documentation** - Every public member needs docs
❌ **Don't use `precision: 10` for floats** - Too strict, causes flaky tests
❌ **Don't write single-assertion tests** - Test multiple scenarios per method
❌ **Don't hardcode magic numbers** - Use named variables for clarity
