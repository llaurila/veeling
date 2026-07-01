using Veeling.CLI.Exceptions;
using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class ServiceFailureTests
{
    [Fact]
    public void Publish_WithoutMasterData_ThrowsProjectPublishException()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.ServiceTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], masterLanguage: "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory);
            TestProjectFactory.EnsureDataDirectory(projectDirectory);

            Project project = new(Path.Combine(projectDirectory.FullName, Project.ProjectFileName));
            ProjectPublisher publisher = new();

            ProjectPublishException ex = Assert.Throws<ProjectPublishException>(() => publisher.Publish(project));
            Assert.Contains("master language 'en' does not exist", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void TranslationJob_InvalidJson_ThrowsTranslationResponseException()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter =>
            {
                if (recordFilter.ToString() == "Schema1.*:en")
                {
                    return [new DataRetrieveResult(
                        new DataModel { Name = "Field1", Value = "Hello" },
                        new RecordLocator("Schema1", "Field1", "en"))];
                }

                return [];
            }
        };

        TranslationJob job = new(
            session,
            new InvalidJsonLLMProvider(),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier());

        TranslationResponseException ex = Assert.Throws<TranslationResponseException>(() => job.Execute());
        Assert.Contains("translated JSON is invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, session.SaveChangesCalls);
    }

    [Fact]
    public void TranslationJob_EmptyResponse_ThrowsTranslationResponseException()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter =>
            {
                if (recordFilter.ToString() == "Schema1.*:en")
                {
                    return [new DataRetrieveResult(
                        new DataModel { Name = "Field1", Value = "Hello" },
                        new RecordLocator("Schema1", "Field1", "en"))];
                }

                return [];
            }
        };

        TranslationJob job = new(
            session,
            new EmptyResponseLLMProvider(),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier());

        TranslationResponseException ex = Assert.Throws<TranslationResponseException>(() => job.Execute());
        Assert.Contains("empty translation response", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, session.SaveChangesCalls);
    }

    [Fact]
    public void TranslationJob_PartialTranslationMap_ThrowsTranslationResponseException()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.ServiceTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], masterLanguage: "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1", "Field2");

            Project project = new(projectFile.FullName);
            MockProjectDataSession session = new(project)
            {
                OnGet = recordFilter =>
                {
                    if (recordFilter.ToString() == "Schema1.*:en")
                    {
                        return
                        [
                            new DataRetrieveResult(
                                new DataModel { Name = "Field1", Value = "Hello" },
                                new RecordLocator("Schema1", "Field1", "en")),
                            new DataRetrieveResult(
                                new DataModel { Name = "Field2", Value = "World" },
                                new RecordLocator("Schema1", "Field2", "en"))
                        ];
                    }

                    return [];
                }
            };

            TranslationJob job = new(
                session,
                new PartialMapLLMProvider(),
                project,
                "Schema1",
                new Language("en"),
                new Language("fi"),
                new ProviderAuthFailureClassifier());

            TranslationResponseException ex = Assert.Throws<TranslationResponseException>(() => job.Execute());

            Assert.Contains("missing required fields", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Field2", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0, session.SaveChangesCalls);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void TranslationJob_ProviderAuthFailure_ThrowsProviderAuthenticationException()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter =>
            {
                if (recordFilter.ToString() == "Schema1.*:en")
                {
                    return [new DataRetrieveResult(
                        new DataModel { Name = "Field1", Value = "Hello" },
                        new RecordLocator("Schema1", "Field1", "en"))];
                }

                return [];
            }
        };

        TranslationJob job = new(
            session,
            new AuthFailureLLMProvider(),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier());

        ProviderAuthenticationException ex = Assert.Throws<ProviderAuthenticationException>(() => job.Execute());

        Assert.Equal(4, ex.ExitCode);
        Assert.Equal(0, session.SaveChangesCalls);
    }

    [Fact]
    public void TranslationJob_ProviderFailure_ThrowsProviderExecutionException()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter =>
            {
                if (recordFilter.ToString() == "Schema1.*:en")
                {
                    return [new DataRetrieveResult(
                        new DataModel { Name = "Field1", Value = "Hello" },
                        new RecordLocator("Schema1", "Field1", "en"))];
                }

                return [];
            }
        };

        TranslationJob job = new(
            session,
            new ProviderFailureLLMProvider(),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier());

        ProviderExecutionException ex = Assert.Throws<ProviderExecutionException>(() => job.Execute());

        Assert.Equal(3, ex.ExitCode);
        Assert.Equal(0, session.SaveChangesCalls);
    }

    [Fact]
    public void TranslationJob_SourceMissingRequiredField_ThrowsMissingSourceDataException()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.ServiceTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], masterLanguage: "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1", "Field2");

            Project project = new(projectFile.FullName);
            MockProjectDataSession session = new(project)
            {
                OnGet = recordFilter =>
                {
                    if (recordFilter.ToString() == "Schema1.*:en")
                    {
                        return
                        [
                            new DataRetrieveResult(
                                new DataModel { Name = "Field1", Value = "Hello" },
                                new RecordLocator("Schema1", "Field1", "en"))
                        ];
                    }

                    return [];
                }
            };

            TranslationJob job = new(
                session,
                new SuccessfulLLMProvider(),
                project,
                "Schema1",
                new Language("en"),
                new Language("fi"),
                new ProviderAuthFailureClassifier());

            MissingSourceDataException ex = Assert.Throws<MissingSourceDataException>(() => job.Execute());

            Assert.Equal(5, ex.ExitCode);
            Assert.Contains("Missing fields", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Field2", ex.Message, StringComparison.Ordinal);
            Assert.Equal(0, session.SaveChangesCalls);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void TranslationJob_SaveFailure_ThrowsPersistenceExceptionWithContext()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            SaveChangesException = new InvalidOperationException("disk full"),
            OnGet = recordFilter =>
            {
                if (recordFilter.ToString() == "Schema1.*:en")
                {
                    return [new DataRetrieveResult(
                        new DataModel { Name = "Field1", Value = "Hello" },
                        new RecordLocator("Schema1", "Field1", "en"))];
                }

                if (recordFilter.ToString() == "Schema1.Field1:fi")
                {
                    return [new DataRetrieveResult(
                        null,
                        new RecordLocator("Schema1", "Field1", "fi"))];
                }

                return [];
            }
        };

        TranslationJob job = new(
            session,
            new SuccessfulLLMProvider(),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier());

        PersistenceException ex = Assert.Throws<PersistenceException>(() => job.Execute());

        Assert.Contains("Failed to persist translated records", ex.Message, StringComparison.Ordinal);
        Assert.Equal("disk full", ex.InnerException?.Message);
    }

    private sealed class InvalidJsonLLMProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, "not-json");
        }
    }

    private sealed class SuccessfulLLMProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, "{\"Field1\":\"Hei\"}");
        }
    }

    private sealed class EmptyResponseLLMProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, "   ");
        }
    }

    private sealed class PartialMapLLMProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, "{\"Field1\":\"Hei\"}");
        }
    }

    private sealed class ProviderFailureLLMProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            throw new InvalidOperationException("Provider timeout");
        }
    }

    private sealed class AuthFailureLLMProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            throw new InvalidOperationException("Unauthorized: invalid api key");
        }
    }
}
