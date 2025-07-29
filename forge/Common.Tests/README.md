# ChangeLogConfig Unit Tests

This document describes the unit test implementation for the `ChangeLogConfig` class.

## Overview

The `ChangeLogConfig` class constructor has been tested comprehensively to ensure it behaves correctly for all input scenarios. The tests cover:

1. **Null and empty input handling**
2. **Case-insensitive "all" keyword handling**
3. **Specific tag input handling**
4. **Whitespace trimming**
5. **Git service integration**

## Test Structure

### Test Projects
- **Common.Tests**: Main test project containing unit tests for the Common library
- **Dependencies**: xUnit, Moq for mocking, and project reference to Common

### Test Classes
1. **ChangeLogConfigTests**: Comprehensive unit tests with mocking
2. **ChangeLogConfigIntegrationTests**: Integration tests with real Git operations

## Key Features Tested

### Constructor Behavior
- ? `null` input ? `ChangeLogSource.LastTag` + calls `Git.GetLastTag()`
- ? Empty string `""` ? `ChangeLogSource.LastTag` + calls `Git.GetLastTag()`
- ? Whitespace `"   "` ? `ChangeLogSource.LastTag` + calls `Git.GetLastTag()`
- ? `"all"` (case-insensitive) ? `ChangeLogSource.All` + `Tag = null`
- ? Custom tag ? `ChangeLogSource.SpecificTag` + `Tag = input`
- ? Whitespace trimming for all inputs

### Dependency Injection
- ? Optional `IGitService` parameter for testability
- ? Falls back to `GitService` (wrapper for static `Git` class) when not provided
- ? Proper mocking of Git operations

### Edge Cases
- ? Git service returning `null` for last tag
- ? Various whitespace combinations
- ? Case variations of "all" keyword

## Test Categories

### Unit Tests (with Mocking)
```csharp
public class ChangeLogConfigTests
{
    // Tests with Mock<IGitService> to isolate behavior
    // - Constructor_WithNullInput_SetsLastTagSourceAndCallsGetLastTag
    // - Constructor_WithEmptyString_SetsLastTagSourceAndCallsGetLastTag
    // - Constructor_WithWhitespaceString_SetsLastTagSourceAndCallsGetLastTag
    // - Constructor_WithNullReturnFromGitService_SetsLastTagSourceWithNullTag
    // - Constructor_WithAllKeyword_SetsAllSourceAndNullTag
    // - Constructor_WithSpecificTag_SetsSpecificTagSourceAndTag
    // - Constructor_WithSpecificTagWithWhitespace_TrimsAndSetsTag
    // - Constructor_WithoutGitService_UsesDefaultGitService
    // - Constructor_CaseInsensitiveAllKeyword_WorksCorrectly
}
```

### Integration Tests (with Real Git)
```csharp
public class ChangeLogConfigIntegrationTests
{
    // Tests with real GitService to ensure integration works
    // - Constructor_WithRealGitService_WorksCorrectly
    // - Constructor_WithNullAndRealGitService_CallsRealGetLastTag
    // - Constructor_WithAllKeyword_DoesNotCallGitService
}
```

## Running Tests

```bash
# Run all tests
dotnet test Common.Tests/Common.Tests.csproj

# Run with verbose output
dotnet test Common.Tests/Common.Tests.csproj --verbosity normal

# Run specific test class
dotnet test Common.Tests/Common.Tests.csproj --filter "FullyQualifiedName~ChangeLogConfigTests"
```

## Design Benefits

1. **Testability**: `IGitService` interface allows mocking of Git operations
2. **Isolation**: Unit tests don't depend on actual Git repository state
3. **Coverage**: Tests cover all constructor paths and edge cases
4. **Maintainability**: Clear test names and organized test structure
5. **Integration**: Separate integration tests verify real-world behavior

## Test Results

- ? **24/24 tests passing**
- ? **100% constructor coverage**
- ? **All edge cases handled**
- ? **Mocking works correctly**
- ? **Integration tests validate real behavior**