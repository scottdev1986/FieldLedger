\set ON_ERROR_STOP on

-- Run as postgres (or another role that can seed fixtures and SET ROLE) after
-- all migrations have been applied. Fixed UUIDs are safe because every change
-- is rolled back at the end of this transaction.
begin;

insert into users (id, email, display_name, password_hash)
values
  ('f1ed0000-0000-4000-8000-000000000001', 'rls-owner-a@fieldledger.test', 'RLS Owner A', 'not-a-real-password-hash'),
  ('f1ed0000-0000-4000-8000-000000000002', 'rls-owner-b@fieldledger.test', 'RLS Owner B', 'not-a-real-password-hash');

insert into organizations (id, name, slug, created_by)
values
  (
    'f1ed1000-0000-4000-8000-000000000001',
    'RLS Test Organization A',
    'rls-test-organization-a',
    'f1ed0000-0000-4000-8000-000000000001'
  ),
  (
    'f1ed1000-0000-4000-8000-000000000002',
    'RLS Test Organization B',
    'rls-test-organization-b',
    'f1ed0000-0000-4000-8000-000000000002'
  );

-- User A is a viewer in Organization B; User B remains a non-member of A.
insert into organization_members (organization_id, user_id, role)
values (
  'f1ed1000-0000-4000-8000-000000000002',
  'f1ed0000-0000-4000-8000-000000000001',
  'viewer'
);

insert into seasons (id, organization_id, year, name, starts_on, ends_on)
values (
  'f1ed2000-0000-4000-8000-000000000002',
  'f1ed1000-0000-4000-8000-000000000002',
  2026,
  'RLS Test 2026',
  date '2026-01-01',
  date '2026-12-31'
);

insert into fields (id, organization_id, name, acreage, default_crop)
values
  ('f1ed3000-0000-4000-8000-000000000021', 'f1ed1000-0000-4000-8000-000000000002', 'RLS Field One', 10, 'corn'),
  ('f1ed3000-0000-4000-8000-000000000022', 'f1ed1000-0000-4000-8000-000000000002', 'RLS Field Two', 20, 'soybean'),
  ('f1ed3000-0000-4000-8000-000000000023', 'f1ed1000-0000-4000-8000-000000000002', 'RLS Field Three', 30, 'wheat');

set local role authenticated;

select set_config(
  'app.user_id',
  'f1ed0000-0000-4000-8000-000000000001',
  true
);

do $$
begin
  if (
    select count(*)
    from organizations
    where id = 'f1ed1000-0000-4000-8000-000000000001'
  ) <> 1 then
    raise exception 'assertion failed: member cannot see own organization';
  end if;
end;
$$;

-- User B is not a member of Organization A and must see no row for it.
select set_config(
  'app.user_id',
  'f1ed0000-0000-4000-8000-000000000002',
  true
);

do $$
begin
  if (
    select count(*)
    from organizations
    where id = 'f1ed1000-0000-4000-8000-000000000001'
  ) <> 0 then
    raise exception 'assertion failed: non-member can see another organization';
  end if;
end;
$$;

-- User A is only a viewer in Organization B.
select set_config(
  'app.user_id',
  'f1ed0000-0000-4000-8000-000000000001',
  true
);

do $$
declare
  v_denied boolean := false;
begin
  begin
    insert into activities (
      organization_id,
      field_id,
      season_id,
      activity_type,
      activity_date,
      notes,
      created_by
    )
    values (
      'f1ed1000-0000-4000-8000-000000000002',
      'f1ed3000-0000-4000-8000-000000000021',
      'f1ed2000-0000-4000-8000-000000000002',
      'note',
      date '2026-07-09',
      'viewer write must fail',
      'f1ed0000-0000-4000-8000-000000000001'
    );
  exception
    when insufficient_privilege then
      v_denied := true;
  end;

  if not v_denied then
    raise exception 'assertion failed: viewer inserted an activity';
  end if;
end;
$$;

do $$
declare
  v_denied boolean := false;
begin
  begin
    perform app.set_org_plan(
      'f1ed1000-0000-4000-8000-000000000002',
      'pro'
    );
  exception
    when raise_exception then
      if sqlerrm <> 'only an organization owner can change the plan' then
        raise;
      end if;
      v_denied := true;
  end;

  if not v_denied then
    raise exception 'assertion failed: non-owner changed the organization plan';
  end if;
end;
$$;

do $$
declare
  v_denied boolean := false;
begin
  begin
    update entitlements
    set plan = 'pro'
    where organization_id = 'f1ed1000-0000-4000-8000-000000000002';
  exception
    when insufficient_privilege then
      v_denied := true;
  end;

  if not v_denied then
    raise exception 'assertion failed: authenticated role directly updated entitlements';
  end if;
end;
$$;

-- User B owns Organization B but its Free plan permits only three active fields.
select set_config(
  'app.user_id',
  'f1ed0000-0000-4000-8000-000000000002',
  true
);

do $$
declare
  v_denied boolean := false;
begin
  begin
    insert into fields (organization_id, name, acreage, default_crop)
    values (
      'f1ed1000-0000-4000-8000-000000000002',
      'RLS Field Four',
      40,
      'corn'
    );
  exception
    when raise_exception then
      if sqlerrm <> 'field_limit_reached' then
        raise;
      end if;
      v_denied := true;
  end;

  if not v_denied then
    raise exception 'assertion failed: fourth active field was created on Free plan';
  end if;
end;
$$;

rollback;
