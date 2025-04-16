```markdown
# Redis Quiz API

This is a simple quiz backend built with **.NET 8**, **Redis**, and **Minimal API**.  
It supports adding members, answering questions, and getting real-time updates using Redis Pub/Sub.

## Features

- Add new members
- Get quiz questions
- Answer questions and gain points
- Prevent double answers
- Auto-expiring questions
- Live updates using Redis channels

## Technologies

- ASP.NET Core (.NET 8)
- Redis (Hash, List, Set, Pub/Sub)
- Minimal API
- Newtonsoft.Json
- Swagger (for testing)

## Endpoints

### `GET /`  
Pre-loads some sample questions into Redis.

### `POST /add-member/{name}`  
Adds a new member to the quiz.

### `GET /members`  
Returns the list of all members with their scores.

### `GET /question/{id}`  
Returns the text of the question by its ID.

### `PATCH /answer/{question}/{name}/{answer}`  
Checks the answer. If correct and not answered before — adds 1 point to the user.

## Run the project

1. Make sure you have Redis running locally (default port: `6379`)
2. Run the app:

```bash
dotnet run
```

3. Open Swagger at [http://localhost:5000/swagger](http://localhost:5000/swagger) to test the API.

## Example Pub/Sub

The app subscribes to `quiz:updates` channel in Redis and prints messages to the console, for example:

```
[Pub/Sub] Fedia gains 1 point for answering question #2.
```

---

### Member format

```json
{
  "name": "Fedia",
  "score": 3
}
```
