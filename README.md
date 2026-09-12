# Discord bot

## Installation
Install via https://api.darketomaly.com/install-discord-bot

## Usage
### Commands
`/darksendmsg` Sends a message by the bot to a specific channel.<br>
`/darkeditmsg` Very useful when you have sent a message but want to edit the attached image. This is only possible with a webhook, and the webhook has the limitation of having a fixed profile picture and name, so it can grow old quickly for pinned/unique messages.<br>

<hr>

# Discord relays
## Jira  relay
Sends Jira json data as a formatted embed to a specific discord channel.

<img width="417" height="129" alt="image" src="https://github.com/user-attachments/assets/294e7dd7-17d9-4bda-a609-3113f7def342" />

1. Configure your automations using these specific names:
```
 discord-issue-approved
 discord-issue-created
 discord-issue-ready-for-review
 discord-issue-rejected
 discord-issue-revision
 discord-issue-start
 discord-sprint-completed
 discord-sprint-start
 discord-version-released
 ```
2. All the automations must send this data:
```
 {
    "event": "{{rule.name}}",
    "initiator_display_name" : "{{initiator.displayName}}",
    "issue_key" : "{{issue.key}}",
    "issue_name" : "{{issue.summary.jsonEncode}}",
    "initiator_icon" : "{{initiator.avatarUrls."48x48"}}",
    "version_released" : "{{version.name}}",
    "sprint_name" : "{{sprint.name}}",
    "rejection_reason" : "{{issue.customfield_10044.jsonEncode()}}"
 }
 ```
 3. Configure the automation request url to https://api.darketomaly.com/jira-discord-webhook?channel=desiredchannelid (replace desiredchannelid by your channel id).

## Plastic SCM relay
Sends Plastic SCM json data as a formatted embed to a specific discord channel.

<img width="248" height="150" alt="image" src="https://github.com/user-attachments/assets/982f45ae-836d-4cc3-be22-5894532505a2" />

1. On the Unity Dashboard, go to Version control -> Settings -> Integrations -> Webhook -> Add new webhook
2. Set payload url https://api.darketomaly.com/plastic-discord-webhook?channel=desiredchannelid (replace desiredchannelid by your channel id).
3. Select which repository you want the webhook to act on. Selecting all repositories also works.
4. Select these events (all "after", not "before"): Branch created, check in, label created and repository created.

## Github relay

1. Go to your GitHub repository -> Settings -> Webhooks -> Add webhook.
2.  You can select the desired events, though usually the push event alone is enough.
3.  Select content type as application/json with payload URL https://api.darketomaly.com/git-discord-webhook?channel=desiredchannel id (replace desiredchannelid by your channel id).
