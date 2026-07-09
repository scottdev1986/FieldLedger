"use client";

import { Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { TooltipProps } from "recharts";
import type { NameType, ValueType } from "recharts/types/component/DefaultTooltipContent";
import { Card, Overline } from "@/components/ui/Primitives";
import type { InsightsResponse } from "@/lib/api-types";
import { formatCurrency, formatNumber } from "@/lib/format";

const colors = ["#16A34A", "#6366F1", "#0EA5E9", "#F59E0B", "#EC4899", "#14B8A6"];
const axis = { tick: { fill: "#71717A", fontSize: 11 }, axisLine: false, tickLine: false } as const;

function ChartCard({ title, subtitle, label, children }: { title: string; subtitle: string; label: string; children: React.ReactNode }) {
  return <Card className="chart-card"><Overline>{title}</Overline><p className="mt-2 text-xs text-ink-soft">{subtitle}</p><div aria-label={label} className="mt-4 h-[280px] w-full" role="img">{children}</div></Card>;
}

type LedgerTooltipProps = TooltipProps<ValueType, NameType> & { currency?: boolean };

function LedgerTooltip({ active, payload, label, currency = false }: LedgerTooltipProps) {
  return active && payload?.length ? <div className="min-w-36 rounded-md border border-line bg-surface p-3 text-xs shadow-pop"><p className="mb-2 font-medium text-ink">{label}</p>{payload.map((item, index) => <div className="mt-1 flex items-center gap-2" key={`${item.name}-${index}`}><span className="h-2 w-2 rounded-full" style={{ background: item.color }} /><span className="text-ink-soft">{item.name}</span><span className="ml-auto font-mono text-ink">{typeof item.value === "number" ? currency ? formatCurrency(item.value, true) : formatNumber(item.value) : Array.isArray(item.value) ? item.value.join("–") : item.value}</span></div>)}</div> : null;
}

export function InsightsCharts({ data }: { data: InsightsResponse }) {
  const crops = [...new Set(data.yieldBySeason.map((item) => item.crop))];
  const yieldRows = [...new Set(data.yieldBySeason.map((item) => item.year))].sort().map((year) => ({ year, ...Object.fromEntries(data.yieldBySeason.filter((item) => item.year === year).map((item) => [item.crop, item.yieldPerAcre])) }));
  return <div className="grid gap-4 lg:grid-cols-2">
    <ChartCard label="Grouped bars compare yield per acre for each crop over all reported seasons." subtitle="Harvest quantity divided by planted acreage." title="Yield / acre by season"><ResponsiveContainer height="100%" width="100%"><BarChart barCategoryGap="28%" data={yieldRows}><CartesianGrid stroke="#E4E4E7" vertical={false} /><XAxis dataKey="year" {...axis} /><YAxis tickFormatter={(value: number) => `${value} bu`} width={48} {...axis} /><Tooltip content={(props) => <LedgerTooltip {...props} />} cursor={{ fill: "#F4F4F5" }} />{crops.map((crop, index) => <Bar isAnimationActive={false} dataKey={crop} fill={colors[index % colors.length]} key={crop} maxBarSize={40} radius={[4,4,0,0]} />)}<Legend iconSize={8} iconType="circle" wrapperStyle={{ fontSize: 12 }} /></BarChart></ResponsiveContainer></ChartCard>
    <ChartCard label="Two lines compare input costs and harvest value across seasons." subtitle="A clean view of seasonal margin movement." title="Input cost vs harvest value"><ResponsiveContainer height="100%" width="100%"><LineChart data={data.costVsValue}><CartesianGrid stroke="#E4E4E7" vertical={false} /><XAxis dataKey="year" {...axis} /><YAxis tickCount={5} tickFormatter={(value: number) => formatCurrency(value, true)} width={56} {...axis} /><Tooltip content={(props) => <LedgerTooltip {...props} currency />} cursor={{ stroke: "#D4D4D8", strokeWidth: 1 }} /><Line isAnimationActive={false} dataKey="inputCost" name="Input cost" dot={false} activeDot={{ r: 3 }} stroke={colors[1]} strokeWidth={2} /><Line isAnimationActive={false} dataKey="harvestValue" name="Harvest value" dot={false} activeDot={{ r: 3 }} stroke={colors[0]} strokeWidth={2} /><Legend iconSize={8} iconType="circle" wrapperStyle={{ fontSize: 12 }} /></LineChart></ResponsiveContainer></ChartCard>
    <ChartCard label="Donut chart shows the share of active acreage planted to each crop." subtitle="Current acreage allocation by crop." title="Crop mix by acreage"><ResponsiveContainer height="100%" width="100%"><PieChart><Pie isAnimationActive={false} data={data.cropMix} dataKey="acreage" innerRadius="60%" nameKey="crop" outerRadius="82%" stroke="#FFFFFF" strokeWidth={2}>{data.cropMix.map((item, index) => <Cell fill={colors[index % colors.length]} key={item.crop} />)}</Pie><Tooltip content={(props) => <LedgerTooltip {...props} />} /><Legend align="right" iconSize={8} iconType="circle" layout="vertical" verticalAlign="middle" wrapperStyle={{ fontSize: 12 }} /></PieChart></ResponsiveContainer></ChartCard>
    <ChartCard label="Horizontal bars rank fields by net value for the selected season." subtitle="Harvest value less recorded input cost." title="Field net value"><ResponsiveContainer height="100%" width="100%"><BarChart data={data.fieldNetValue} layout="vertical" margin={{ left: 8, right: 24 }}><CartesianGrid horizontal={false} stroke="#E4E4E7" /><XAxis type="number" tickFormatter={(value: number) => formatCurrency(value, true)} {...axis} /><YAxis dataKey="fieldName" type="category" width={96} {...axis} /><Tooltip content={(props) => <LedgerTooltip {...props} currency />} cursor={{ fill: "#F4F4F5" }} /><Bar isAnimationActive={false} dataKey="netValue" name="Net value" fill={colors[0]} maxBarSize={28} radius={[0,4,4,0]} /></BarChart></ResponsiveContainer></ChartCard>
  </div>;
}
