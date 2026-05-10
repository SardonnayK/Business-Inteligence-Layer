interface StatCardProps {
  label: string
  value: string | number
  subtext?: string
  icon?: React.ReactNode
}

export default function StatCard({ label, value, subtext, icon }: StatCardProps) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm font-medium text-gray-500">{label}</p>
          <p className="mt-1 text-3xl font-semibold text-gray-900">{value}</p>
          {subtext && <p className="mt-1 text-xs text-gray-400">{subtext}</p>}
        </div>
        {icon && (
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
            {icon}
          </div>
        )}
      </div>
    </div>
  )
}
