# Discord bot

## Installation
Install via https://api.darketomaly.com/install-discord-bot

## Website contact email

The `POST /contact` endpoint accepts the website contact form as
`multipart/form-data` and sends one branded HTML email per submission through
the Resend API.

Configure these Railway environment variables:

```text
RESEND_API_KEY=re_...
CONTACT_EMAIL_FROM=Website <website@your-verified-domain.com>
CONTACT_EMAIL_TO=your-private-email@example.com
CONTACT_EMAIL_LOGO_URL=https://darketomaly.com/darketomaly_profile_picture.png
WEBSITE_ORIGIN=https://darketomaly.com
```

`CONTACT_EMAIL_FROM` must use a domain verified with Resend. Add the SPF and
DKIM records Resend provides before testing delivery. `WEBSITE_ORIGIN` limits
browser requests to the website while the endpoint remains publicly reachable
for HTTP clients.

## Usage

### Commands
`/darksendmsg` Sends a message by the bot to a specific channel.<br>
`/darkeditmsg` Very useful when you have sent a message but want to edit the attached image. This is only possible with a webhook, and the webhook has the limitation of having a fixed profile picture and name, so it can grow old quickly for pinned/unique messages.<br>

Use `/darkautoreactthumbs` or `darkautoreactlaugh` to have the bot automatically react to messages.

<img width="565" height="185" alt="image" src="https://github.com/user-attachments/assets/78351d0b-5ebe-406b-ad66-516c8ef40942" />

<br>Use `/darksendreactforrolemsg` to send a bot message with automatic emoji reaction. When users click on this reaction, they get assigned or unassigned roles. Up to three roles can be chosen on the command. You can also provide a message id to this command to replace a sent "react for role" message, which updates the message and removes/add the appropriate reactions.

<img width="404" height="256" alt="image" src="https://github.com/user-attachments/assets/dc80c58d-bc19-4ef6-a54e-27efac9eef83" />

<hr>

# Discord relays

## Secret
All payloads must provide `channelid` and `secret`. To generate a secret, a discord server admin should use the command `darkgenerateserverkey`.

<img width="542" height="100" alt="image" src="https://github.com/user-attachments/assets/6c5e0931-4ec4-44ba-a961-86450daab493" />

This generates a unique secret string that only server admins can see. This avoids someone from your server abusing the api and redirecting bot messages towards a channel.

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
 3. Configure the automation request url to https://api.darketomaly.com/jira-discord-webhook?channel=XXX&secret=XXX

## Plastic SCM relay
Sends Plastic SCM json data as a formatted embed to a specific discord channel.

<img width="248" height="150" alt="image" src="https://github.com/user-attachments/assets/982f45ae-836d-4cc3-be22-5894532505a2" />

1. On the Unity Dashboard, go to Version control -> Settings -> Integrations -> Webhook -> Add new webhook
2. Set payload url https://api.darketomaly.com/plastic-discord-webhook?channel=XXX&secret=XXX
3. Select which repository you want the webhook to act on. Selecting all repositories also works.
4. Select these events (all "after", not "before"): Branch created, check in, label created and repository created.

## Github relay

<img width="521" height="226" alt="image" src="https://github.com/user-attachments/assets/c4f80da3-fd18-46af-9ec4-59e9a417fe96" />

1. Go to your GitHub repository -> Settings -> Webhooks -> Add webhook.
2.  You can select the desired events, though usually the push event alone is enough.
3.  Select content type as application/json with payload URL https://api.darketomaly.com/git-discord-webhook?channel=XXX&secret=XXX
