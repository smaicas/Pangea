-- PangeaSupabaseApp - schema, policies and grants.
--
-- Paste into the SQL editor of a new Supabase project and run it once. Written to be run again:
-- every object is created if it does not exist, and every policy is dropped before it is created.

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------------------------------
-- The table
-- ---------------------------------------------------------------------------------------------

create table if not exists public.notes (
    -- Assigned by the client, so a note written offline keeps its identity when it syncs. That is
    -- what lets the same row be sent twice without becoming two notes.
    id         uuid primary key,
    owner_id   uuid        not null references auth.users (id) on delete cascade,
    title      text        not null check (length(btrim(title)) between 1 and 120),
    created_at timestamptz not null default now()
);

create index if not exists notes_by_owner on public.notes (owner_id, created_at desc);

-- ---------------------------------------------------------------------------------------------
-- Row level security
--
-- On, and with a policy per operation. A table with RLS enabled and no policy is readable by
-- nobody through the anon key, which is the right thing to fall back to.
--
-- Every policy here compares against the row itself. A policy that reads another table is subject
-- to that table's policies, which is how a write ends up rejected for a row the caller cannot see
-- yet - reach for a SECURITY DEFINER function when you need that.
-- ---------------------------------------------------------------------------------------------

alter table public.notes enable row level security;

drop policy if exists notes_read on public.notes;
create policy notes_read on public.notes for select to authenticated
    using (owner_id = auth.uid());

drop policy if exists notes_write on public.notes;
create policy notes_write on public.notes for insert to authenticated
    with check (owner_id = auth.uid());

drop policy if exists notes_edit on public.notes;
create policy notes_edit on public.notes for update to authenticated
    using (owner_id = auth.uid()) with check (owner_id = auth.uid());

drop policy if exists notes_delete on public.notes;
create policy notes_delete on public.notes for delete to authenticated
    using (owner_id = auth.uid());

-- ---------------------------------------------------------------------------------------------
-- Privileges
--
-- Row level security decides which rows a request may touch. It does not grant the right to touch
-- the table at all - that is an ordinary GRANT, and it is checked first. A table with perfect
-- policies and no grant answers:
--
--   42501: permission denied for table notes
--
-- Supabase normally hands these out through ALTER DEFAULT PRIVILEGES, but those only apply to
-- tables created by the role they were set for. A table created from the SQL editor can end up
-- owned by another role and miss them entirely.
--
-- Granted to `authenticated` and not to `anon`: an anonymous Supabase account still carries the
-- `authenticated` role, with `is_anonymous` true inside its token. `anon` is the role of a request
-- with no session at all.
-- ---------------------------------------------------------------------------------------------

grant usage on schema public to anon, authenticated;

grant select, insert, update, delete on all tables in schema public to authenticated;
grant execute on all functions in schema public to authenticated;

-- And the same for anything added later, so a new table is not a new outage.
alter default privileges in schema public
    grant select, insert, update, delete on tables to authenticated;

alter default privileges in schema public
    grant execute on functions to authenticated;

-- ---------------------------------------------------------------------------------------------
-- Realtime - what a second device has to hear about without being asked
-- ---------------------------------------------------------------------------------------------

do $$
begin
    if not exists (select 1 from pg_publication where pubname = 'supabase_realtime') then
        create publication supabase_realtime;
    end if;

    -- Added only when missing: ALTER PUBLICATION ... ADD TABLE errors on one already published,
    -- which would stop this file being run twice.
    if not exists (
        select 1 from pg_publication_tables
        where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = 'notes'
    ) then
        alter publication supabase_realtime add table public.notes;
    end if;
end;
$$;
