import argparse
from pip._internal.commands import create_command

# Depending on the version of pip/pip-tools this has a different name.
try:
    from pip._internal.utils.misc import get_installed_distributions
    get_installed_dists = get_installed_distributions
except ImportError:
    from piptools.scripts.sync import _get_installed_distributions
    get_installed_dists = _get_installed_distributions

from piptools import sync
from piptools._compat.pip_compat import parse_requirements

def main(requirements):
    """ Get installed pip packages, compare them to the passed packages `requirements` file,
        install missing packages, uninstall packages not needed anymore
    """    
    install_command = create_command("install")
    options, _ = install_command.parse_args([])
    session = install_command._build_session(options)
    finder = install_command._build_package_finder(options=options, session=session)

    requirements = parse_requirements(args.requirement_file, finder=finder, session=session)
    
    installed_dists = get_installed_dists(paths=[args.site_path])
    to_install, to_uninstall = sync.diff(requirements, installed_dists)
    sync.sync(to_install, to_uninstall)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('requirement_file', help='requirements.txt file')
    # Don't pick up the system pacakges, only the ones in *our* site packages
    parser.add_argument('site_path', help='path to the isolated site-packages')
    args = parser.parse_args()

    main(args)

