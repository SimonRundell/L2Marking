using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using L2Marker.Models;

namespace L2Marker.Services;

/// <summary>
/// Sends one learner's extracted submission text to the Claude API for marking against a unit's
/// model answers, and gets back a structured achieved/not-achieved verdict per criterion.
///
/// Cost control: the unit's marking rubric (instructions + model answers, identical for every
/// learner in a batch) is sent as a cached system block, so only the first call in a run pays full
/// input price for it - the rest read it from Anthropic's prompt cache at a fraction of the cost.
/// A tool-call is forced so the model returns clean structured JSON instead of free-form prose.
/// </summary>
public class ClaudeMarkingService
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    public async Task<StudentMarkingResult> MarkStudentAsync(
        AppSettings settings,
        UnitReference unit,
        string studentFilePath,
        string learnerName,
        CancellationToken ct = default)
    {
        var result = new StudentMarkingResult
        {
            SourceFilePath = studentFilePath,
            LearnerName = learnerName
        };

        string studentText;
        try
        {
            studentText = DocxTextExtractor.ExtractOrderedText(studentFilePath);
        }
        catch (Exception ex)
        {
            result.Error = $"Could not read submission file: {ex.Message}";
            return result;
        }

        if (string.IsNullOrWhiteSpace(studentText))
        {
            result.Error = "No readable text found in the submission (it may be empty, image-only, or corrupt).";
            return result;
        }

        var requestBody = BuildRequestBody(settings, unit, learnerName, studentFilePath, studentText);

        JsonNode? responseJson;
        try
        {
            responseJson = await SendWithRetryAsync(settings, requestBody, ct);
        }
        catch (Exception ex)
        {
            result.Error = $"API call failed: {ex.Message}";
            return result;
        }

        try
        {
            PopulateFromToolResponse(result, unit, responseJson!, settings);
        }
        catch (Exception ex)
        {
            result.Error = $"Could not parse the marking response: {ex.Message}";
        }

        return result;
    }

    private static JsonObject BuildRequestBody(
        AppSettings settings, UnitReference unit, string learnerName, string studentFilePath, string studentText)
    {
        var systemText = BuildSystemPrompt(unit);
        var userText = BuildUserPrompt(learnerName, studentFilePath, studentText);

        var criteriaIds = unit.Criteria.Select(c => c.Id).ToArray();

        var inputSchema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["criteria"] = new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = criteriaIds.Length,
                    ["maxItems"] = criteriaIds.Length,
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["id"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray(criteriaIds.Select(id => JsonValue.Create(id) as JsonNode).ToArray())
                            },
                            ["achieved"] = new JsonObject { ["type"] = "boolean" },
                            ["comment"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "One short sentence justifying the decision, addressed to the learner as 'you' - never by name, never 'the learner'/'the student'."
                            }
                        },
                        ["required"] = new JsonArray("id", "achieved", "comment")
                    }
                },
                ["overall_achieved"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "True only if every single criterion was achieved."
                },
                ["overall_feedback"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "2-4 sentence summary addressed to the learner as 'you', honest but encouraging."
                },
                ["further_actions"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "What you (the learner) must correct/resubmit, addressed as 'you'. 'None - full pass.' if fully achieved."
                }
            },
            ["required"] = new JsonArray("criteria", "overall_achieved", "overall_feedback", "further_actions")
        };

        var body = new JsonObject
        {
            ["model"] = settings.Model,
            ["max_tokens"] = settings.MaxOutputTokens,
            ["temperature"] = 0,
            ["system"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = systemText,
                    ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
                }
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "submit_marking",
                    ["description"] = "Submit the achieved/not-achieved marking decision for every criterion, plus overall feedback.",
                    ["input_schema"] = inputSchema
                }
            },
            ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = "submit_marking" },
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = userText
                }
            }
        };

        return body;
    }

    private static string BuildSystemPrompt(UnitReference unit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an experienced assessor marking NCFE Level 2 Certificate in Understanding Coding coursework.");
        sb.AppendLine("The learners are 16-19 year old Further Education students. Each unit is a short-answer workbook, " +
                       "and every numbered question is a separate assessment criterion that is marked strictly as ACHIEVED " +
                       "or NOT ACHIEVED - there is no partial credit.");
        sb.AppendLine();
        sb.AppendLine("How to judge each criterion:");
        sb.AppendLine("- Award ACHIEVED when the learner's own answer demonstrates correct, accurate understanding of the " +
                       "concept being asked about, even if it is brief, informally worded, contains grammar or spelling " +
                       "mistakes, or is far less detailed than the model answer. Do not expect model-answer-level depth.");
        sb.AppendLine("- Award NOT ACHIEVED when the answer is missing, merely restates the question without answering it, " +
                       "is factually wrong in a way that undermines the core definition, gives an incorrect example where " +
                       "an example was needed, or is so vague or circular that it does not actually demonstrate understanding.");
        sb.AppendLine("- A minor factual slip inside an otherwise-correct answer does not need to fail the criterion - use " +
                       "judgement about whether the core understanding asked for has been shown.");
        sb.AppendLine("- The model answers below are a reference rubric for what correct understanding looks like. Learners " +
                       "are never expected to match their wording, length, or extra detail.");
        sb.AppendLine();
        sb.AppendLine("For every criterion give a one-sentence comment justifying the decision, addressed directly to the " +
                       "learner as 'you' (e.g. 'You correctly explained...'), never by their name and never as 'the learner' " +
                       "or 'the student'. Then provide overall_feedback in the same second-person voice (2-4 sentences, " +
                       "honest but encouraging, no corporate or robotic tone, avoid cliches), overall_achieved (true only " +
                       "if every criterion passed), and further_actions in the same voice (concretely what to fix/resubmit, " +
                       "or 'None - full pass.' if everything passed).");
        sb.AppendLine();
        sb.AppendLine($"=== {unit.UnitTitle} - CRITERIA ===");
        foreach (var c in unit.Criteria)
            sb.AppendLine($"{c.Id} {c.QuestionText}");
        sb.AppendLine();
        sb.AppendLine("=== MODEL ANSWERS (reference rubric only - not a required format) ===");
        sb.AppendLine(unit.ModelAnswersText);

        return sb.ToString();
    }

    private static string BuildUserPrompt(string learnerName, string studentFilePath, string studentText)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Learner name: {learnerName}");
        sb.AppendLine($"Source file: {Path.GetFileName(studentFilePath)}");
        sb.AppendLine();
        sb.AppendLine("=== LEARNER'S SUBMITTED ANSWERS (extracted text, in document order) ===");
        sb.AppendLine(studentText);
        sb.AppendLine();
        sb.AppendLine("Mark every criterion listed in the rubric and call submit_marking with your decisions.");
        return sb.ToString();
    }

    private async Task<JsonNode?> SendWithRetryAsync(AppSettings settings, JsonObject body, CancellationToken ct)
    {
        const int maxAttempts = 3;
        Exception? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return JsonNode.Parse(responseText);
            }

            var transient = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                             || (int)response.StatusCode >= 500;

            lastError = new Exception($"HTTP {(int)response.StatusCode}: {responseText}");

            if (!transient || attempt == maxAttempts)
                throw lastError;

            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
        }

        throw lastError ?? new Exception("Unknown error calling the Claude API.");
    }

    private static void PopulateFromToolResponse(StudentMarkingResult result, UnitReference unit, JsonNode response, AppSettings settings)
    {
        var usageNode = response["usage"];
        if (usageNode is not null)
        {
            var usage = new ApiUsage(
                InputTokens: usageNode["input_tokens"]?.GetValue<int>() ?? 0,
                OutputTokens: usageNode["output_tokens"]?.GetValue<int>() ?? 0,
                CacheCreationInputTokens: usageNode["cache_creation_input_tokens"]?.GetValue<int>() ?? 0,
                CacheReadInputTokens: usageNode["cache_read_input_tokens"]?.GetValue<int>() ?? 0);
            result.Usage = usage;
            result.EstimatedCostUsd = CostCalculator.CalculateCost(usage, settings);
        }

        var contentArray = response["content"]?.AsArray()
            ?? throw new Exception("Response had no content block.");

        var toolUse = contentArray.FirstOrDefault(c => c?["type"]?.GetValue<string>() == "tool_use")
            ?? throw new Exception("Response did not include a tool_use block (marking may have been refused).");

        var input = toolUse["input"] ?? throw new Exception("tool_use block had no input.");

        var criteriaNode = input["criteria"]?.AsArray() ?? new JsonArray();
        var seenIds = new HashSet<string>();

        foreach (var item in criteriaNode)
        {
            if (item is null) continue;
            var id = item["id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            seenIds.Add(id);
            result.Criteria.Add(new CriterionResult
            {
                Id = id,
                Achieved = item["achieved"]?.GetValue<bool>() ?? false,
                Comment = item["comment"]?.GetValue<string>() ?? ""
            });
        }

        // Defensive: if the model skipped a criterion, add it back as Not Achieved rather than
        // silently dropping it from the marksheet.
        foreach (var c in unit.Criteria)
        {
            if (!seenIds.Contains(c.Id))
            {
                result.Criteria.Add(new CriterionResult
                {
                    Id = c.Id,
                    Achieved = false,
                    Comment = "No marking decision was returned for this criterion - please review manually."
                });
            }
        }

        result.Criteria = result.Criteria
            .OrderBy(c => unit.Criteria.FindIndex(u => u.Id == c.Id))
            .ToList();

        result.OverallAchieved = input["overall_achieved"]?.GetValue<bool>() ?? result.Criteria.All(c => c.Achieved);
        result.OverallFeedback = input["overall_feedback"]?.GetValue<string>() ?? "";
        result.FurtherActions = input["further_actions"]?.GetValue<string>() ?? "";
    }
}
