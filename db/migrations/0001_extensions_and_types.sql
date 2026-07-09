-- Applied once, in filename order, inside a transaction owned by the migration
-- runner. Transaction control intentionally does not appear in migration files.

create extension if not exists pgcrypto;
create extension if not exists citext;

create type member_role as enum ('owner', 'agronomist', 'viewer');
create type plan_tier as enum ('free', 'pro');
create type activity_type as enum (
  'planting',
  'spraying',
  'irrigation',
  'fertilizer',
  'harvest',
  'note'
);
create type crop_type as enum ('corn', 'soybean', 'wheat');
