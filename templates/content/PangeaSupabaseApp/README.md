# PangeaSupabaseApp

An Avalonia application for phones with a shared backend, built on
[Pangea](https://github.com/smaicas/CdCSharp.Pangea) and [Supabase](https://supabase.com).

## Before it runs

The application starts and works without any of this - it draws from its own cache and queues what
it cannot send - but nothing is shared until a project is behind it.

1. **Make a Supabase project.** The database comes with it; there is nothing to create.
2. **Turn on anonymous sign-in.** Authentication → Sign In / Providers → Anonymous Sign-Ins.
   Without it the first launch fails with *"Anonymous sign-ins are disabled"*.
3. **Run `supabase/schema.sql`** in the SQL editor. It is written to be run again after an edit.
4. **Paste the credentials** into `PangeaSupabaseApp/App.axaml.cs`, or pass them when generating:
   `dotnet new pangea-mobile-supabase --SupabaseUrl https://xxx.supabase.co --SupabaseKey sb_publishable_...`

The URL is the **base** project URL, not the REST endpoint: the client appends `/rest/v1` itself.

The anon key is public by design - it identifies the project, not the user, and what a request may
read or write is decided by row level security against the signed-in account. The `service_role`
key is the opposite of that and must never appear in a client.

## What is here

| Project | What it is |
|---|---|
| `PangeaSupabaseApp` | Everything the application is: views, view models, data, theme. |
| `PangeaSupabaseApp.Desktop` | An entry point, so a change can be seen without an emulator. |
<!--#if (Android) -->
| `PangeaSupabaseApp.Android` | The Android head. |
<!--#endif -->
<!--#if (iOS) -->
| `PangeaSupabaseApp.iOS` | The iOS head. |
<!--#endif -->
| `supabase/schema.sql` | The table, its policies, and the grants. |

## The shape of the data layer

Four pieces, and the split is the point:

- **`NotesBackend`** is the only class that knows Supabase exists. A schema change stops there.
- **`NotesCache`** is what the screen draws from. It answers instantly and it answers offline.
- **`NotesRepository`** is what a view model talks to: it reads from the cache and refreshes behind
  it, applies every write locally first, sends it, and queues it when sending fails.
- **`IOutbox`**, from the toolkit, holds what could not be sent until it can.

The user is never waiting on a request and never loses a write. That is the whole reason for the
layering, and it is worth keeping when you replace notes with your own data.

## Two things that will bite you

Both are in `schema.sql`, with the reasoning beside them:

- **Row level security is not a grant.** A table with perfect policies and no `GRANT` answers
  `42501: permission denied`. Postgres checks the privilege first.
- **A policy that reads another table is subject to that table's policies.** Use a
  `SECURITY DEFINER` function, or a write will fail because the row it is checking against is one
  the caller cannot see yet.
