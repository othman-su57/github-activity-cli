using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using var client = new HttpClient
{
  BaseAddress = new Uri("https://api.github.com/users/"),
  Timeout = TimeSpan.FromSeconds(10)
};

client.DefaultRequestHeaders.Add("User-Agent", "MyCliApp/1.0");
return await Run(args);


async Task<int> Run(string[] args)
{
  var req = new HttpClient();

  if (args.Length == 0)
  {
    PrintUsage();
    return 1;
  }
  else
    JsonHandle(await GettingData(args[0]));

  return 0;
}

async Task<string> GettingData(string username)
{
  return await client.GetStringAsync(username + "/events");
}

void JsonHandle(string json)
{
  using JsonDocument doc = JsonDocument.Parse(json);

  string? currentPushRepo = null;
  int pushCount = 0;

  foreach (JsonElement e in doc.RootElement.EnumerateArray())
  {
    string type = e.GetProperty("type").GetString() ?? "";
    string repo = e.GetProperty("repo").GetProperty("name").GetString() ?? "unknown/repo";
    JsonElement payload = e.GetProperty("payload");

    // 1. إذا كان الحدث PushEvent: نجمّعه إذا كان لنفس المستودع السابق
    if (type == "PushEvent")
    {
      if (currentPushRepo == repo)
      {
        pushCount++;
      }
      else
      {
        // إذا كان هناك Push مجمّع لمستودع سابق، نطبعه أولاً قبل بدء العد للمستودع الجديد
        if (currentPushRepo is not null)
        {
          Console.WriteLine(PushedEvent(currentPushRepo, pushCount));
        }

        currentPushRepo = repo;
        pushCount = 1;
      }
      continue;
    }

    // 2. قبل معالجة أي حدث مختلف، نفرّغ (Flush) أي Push مجمّع في الذاكرة
    if (currentPushRepo is not null)
    {
      Console.WriteLine(PushedEvent(currentPushRepo, pushCount));
      currentPushRepo = null;
      pushCount = 0;
    }

    // 3. معالجة بقية الأحداث بالترتيب
    switch (type)
    {
      case "WatchEvent":
        if (payload.TryGetProperty("action", out var watchAction) && watchAction.GetString() == "started")
        {
          Console.WriteLine(StarredEvent(repo));
        }
        break;

      case "IssuesEvent":
        if (payload.TryGetProperty("action", out var issueAction) && issueAction.GetString() == "opened")
        {
          Console.WriteLine(IssueEvent(repo));
        }
        break;

      case "CreateEvent":
        string refType = payload.TryGetProperty("ref_type", out var rt) ? rt.GetString() ?? "item" : "item";
        Console.WriteLine($"- Created a new {refType} in {repo}");
        break;

      case "ForkEvent":
        Console.WriteLine($"- Forked {repo}");
        break;

      case "PullRequestEvent":
        if (payload.TryGetProperty("action", out var prAction) && prAction.GetString() == "opened")
        {
          Console.WriteLine($"- Opened a new pull request in {repo}");
        }
        break;
    }
  }

  // 4. تفريغ أخير (Final Flush) في حال انتهت المصفوفة بـ PushEvent
  if (currentPushRepo is not null)
  {
    Console.WriteLine(PushedEvent(currentPushRepo, pushCount));
  }
}

string PushedEvent(string repo, int count) =>
    $"- Pushed {count} {(count == 1 ? "commit" : "commits")} to {repo}";
string StarredEvent(string repo) =>
    $"- Starred {repo}";

string IssueEvent(string repo) =>
    $"- Opened a new issue in {repo}";
void PrintUsage()
{
  Console.WriteLine("Usage :");
  Console.WriteLine("\tgithub-activity <user-name>");
}
