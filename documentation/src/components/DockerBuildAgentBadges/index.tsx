// documentation/src/components/DockerBuildAgentBadges/index.tsx
import React from 'react';
import ProjectBadges from '../ProjectBadges';

const DockerBuildAgentBadges: React.FC = () => {
  return (
    <ProjectBadges 
      user="the-running-dev" 
      repository="Docker-BuildAgent"
      docsUrl="https://build-agent.subzerodev.com"
      demoUrl="https://barstrad.com"
    />
  );
};

export default DockerBuildAgentBadges;
