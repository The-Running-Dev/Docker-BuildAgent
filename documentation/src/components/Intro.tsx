
import styles from './Intro.module.css';

export default function Intro() {
  return (
    <div className={styles['overview-card']}>
      <div className={styles['overview-subtitle']}>
        Welcome to my technical portfolio! Here you'll find a curated selection of my work across frontend, backend, infrastructure, automation, and open source contributions.
      </div>
      <div className={styles['overview-links']}>
        <a className={styles['overview-link']} href="/docs/projects/frontend/intro">Frontend</a>
        <a className={styles['overview-link']} href="/docs/projects/backend/intro">Backend</a>
        <a className={styles['overview-link']} href="/docs/projects/infrastructure/intro">Infrastructure & DevOps</a>
        <a className={styles['overview-link']} href="/docs/projects/powershell/intro">PowerShell & Automation</a>
        <a className={styles['overview-link']} href="/docs/projects/contributions">Open Source Contributions</a>
      </div>
    </div>
  );
}
