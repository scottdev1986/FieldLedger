-- FieldLedger local role bootstrap.
--
-- The official postgres image creates POSTGRES_DB=fieldledger before it runs
-- docker-entrypoint-initdb.d, so this script configures that database rather
-- than creating it.

do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'authenticated') then
    create role authenticated
      nologin
      nosuperuser
      nocreatedb
      nocreaterole
      noinherit
      noreplication
      nobypassrls;
  else
    alter role authenticated
      nologin
      nosuperuser
      nocreatedb
      nocreaterole
      noinherit
      noreplication
      nobypassrls;
  end if;

  if not exists (select 1 from pg_roles where rolname = 'fieldledger_api') then
    create role fieldledger_api
      login
      password 'fieldledger_api_dev'
      nosuperuser
      nocreatedb
      nocreaterole
      inherit
      noreplication
      nobypassrls;
  else
    alter role fieldledger_api
      login
      password 'fieldledger_api_dev'
      nosuperuser
      nocreatedb
      nocreaterole
      inherit
      noreplication
      nobypassrls;
  end if;

  -- FORCE ROW LEVEL SECURITY also applies to table owners. The migrator is the
  -- intentionally elevated migration/seeding and SECURITY DEFINER owner; API
  -- traffic never uses this role.
  if not exists (select 1 from pg_roles where rolname = 'fieldledger_migrator') then
    create role fieldledger_migrator
      login
      password 'fieldledger_migrator_dev'
      nosuperuser
      nocreatedb
      nocreaterole
      inherit
      noreplication
      bypassrls;
  else
    alter role fieldledger_migrator
      login
      password 'fieldledger_migrator_dev'
      nosuperuser
      nocreatedb
      nocreaterole
      inherit
      noreplication
      bypassrls;
  end if;
end;
$$;

grant authenticated to fieldledger_api;
grant connect on database fieldledger to fieldledger_api;

alter database fieldledger owner to fieldledger_migrator;
grant connect, create, temporary on database fieldledger to fieldledger_migrator;

alter schema public owner to fieldledger_migrator;
grant usage, create on schema public to fieldledger_migrator;
