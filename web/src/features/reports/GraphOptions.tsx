import { useTranslation } from 'react-i18next'
import { CheckList, Switch } from '../../components/ui/Controls'
import type { GraphChoice } from './graphChoice'

export function GraphOptions({ graph }: { graph: GraphChoice }) {
  const { t, i18n } = useTranslation()
  if (graph.capable.length === 0) return <p className="field-hint">{t('reports.graphNone')}</p>
  return (
    <div className="graph-options">
      <Switch checked={graph.on} onChange={(v) => { graph.setOn(v); if (v && graph.chosen.length === 0) graph.setChosen(graph.capable.map((x) => x.code)) }}
        label={t('reports.graphOptionTitle')} hint={t('reports.graphOptionHint')} />
      {graph.on && (
        <CheckList columns={1} selected={graph.chosen} onChange={(next) => graph.setChosen(next.map(String))}
          items={graph.capable.map((x) => ({ id: x.code, label: i18n.language === 'ur' && x.nameUr ? x.nameUr : x.nameEn, sub: x.code }))} />
      )}
    </div>
  )
}
