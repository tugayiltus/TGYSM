// Every icon the UI uses, imported by name so only these ship.
import type { IconName, IconType } from '@astryxdesign/core/Icon';
import {
  ArrowLeft, Bell, BookOpen, Building2, ChartPie, ClipboardList, Contrast, Download, Gauge, History, House, Inbox, Laptop, List,
  Lock, Mail, MailOpen, MapPin, Monitor, MonitorSmartphone, Moon, NotebookText, Pause, Pencil, Plus, Printer, Settings, Sun, Tag, Tags, User,
  Users, UsersRound,
} from 'lucide-react';

/**
 * App icons for Astryx components (ADR-0027): Astryx semantic names where one exists, so the theme can restyle
 * them, else Lucide glyphs (the neutral theme's own set). One concept, one icon across the app.
 */
export const Icons = {
  arrowLeft: ArrowLeft,
  bell: Bell,
  book: BookOpen,
  building: Building2,
  calendar: 'calendar',
  chartPie: ChartPie,
  check: 'check',
  checkCircle: 'success',
  close: 'close',
  contrast: Contrast,
  devices: MonitorSmartphone,
  download: Download,
  envelopeClosed: Mail,
  envelopeOpen: MailOpen,
  group: Users,
  history: History,
  home: House,
  inbox: Inbox,
  laptop: Laptop,
  list: List,
  locationPin: MapPin,
  lock: Lock,
  menu: 'menu',
  moon: Moon,
  notes: NotebookText,
  pause: Pause,
  pencil: Pencil,
  printer: Printer,
  people: UsersRound,
  plus: Plus,
  screenDesktop: Monitor,
  search: 'search',
  settings: Settings,
  speedometer: Gauge,
  sun: Sun,
  tag: Tag,
  tags: Tags,
  task: ClipboardList,
  timer: 'clock',
  upload: 'upload',
  user: User,
  warning: 'warning',
} satisfies Record<string, IconName | IconType>;

export type AppIcon = keyof typeof Icons;
